using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Domain.AppEvent;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Domain.Recording;
using ZLogger;

namespace RadiKeep.Logics.UseCases.Recording;

/// <summary>
/// 録音フローを統括するユースケース
/// </summary>
public class RecordingOrchestrator(
    ILogger<RecordingOrchestrator> logger,
    IEnumerable<IRecordingSource> sources,
    IMediaStorageService storage,
    IMediaTranscodeService transcoder,
    IRecordingRepository repository,
    IRecordingStateEventPublisher recordingStateEventPublisher,
    IAppToastEventPublisher? appToastEventPublisher = null)
{
    /// <summary>
    /// 録音処理を実行する
    /// </summary>
    /// <param name="command">録音コマンド</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>録音結果</returns>
    public async ValueTask<RecordingResult> RecordAsync(RecordingCommand command, CancellationToken cancellationToken = default)
    {
        var source = sources.FirstOrDefault(s => s.CanHandle(command.ServiceKind));
        if (source == null)
        {
            return new RecordingResult(false, null, "未対応のサービスです。");
        }

        Ulid? recordingId = null;
        MediaPath? mediaPath = null;
        var isCommitted = false;

        // 例外が起きても録音フローを中断しないように安全に状態更新を行う
        async ValueTask UpdateStateSafeAsync(RecordingState state, string? message)
        {
            if (recordingId == null)
            {
                return;
            }

            try
            {
                await repository.UpdateStateAsync(recordingId.Value, state, message, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"録音状態の更新に失敗しました。");
                return;
            }

            try
            {
                await recordingStateEventPublisher.PublishAsync(
                    new RecordingStateChangedEvent(
                        recordingId.Value,
                        state,
                        message,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"録音状態変更イベントの通知に失敗しました。");
            }
        }

        /// <summary>
        /// 全画面トースト通知を安全に実行する
        /// </summary>
        async ValueTask PublishGlobalToastSafeAsync(string message, bool isSuccess)
        {
            if (appToastEventPublisher is null)
            {
                return;
            }

            try
            {
                await appToastEventPublisher.PublishAsync(
                    new AppToastEvent(message, isSuccess, DateTimeOffset.UtcNow),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"全画面トーストイベント通知に失敗しました。");
            }
        }

        // コミット前に失敗した場合は一時ファイルを確実に削除する
        async ValueTask CleanupTempSafeAsync()
        {
            if (mediaPath == null || isCommitted)
            {
                return;
            }

            try
            {
                await storage.CleanupTempAsync(mediaPath, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"一時ファイルの削除に失敗しました。");
            }
        }

        try
        {
            var sourceResult = await source.PrepareAsync(command, cancellationToken);
            mediaPath = await storage.PrepareAsync(sourceResult.ProgramInfo, cancellationToken);

            recordingId = await repository.CreateAsync(sourceResult.ProgramInfo, mediaPath, sourceResult.Options, cancellationToken);
            await UpdateStateSafeAsync(RecordingState.Recording, null);

            var ok = await transcoder.RecordAsync(sourceResult, mediaPath, cancellationToken);
            if (!ok)
            {
                var errorMessage = command.IsTimeFree
                    ? "タイムフリー録音チャンク取得に失敗しました。"
                    : command.IsOnDemand
                        ? "聞き逃し配信録音に失敗しました。"
                        : "録音処理に失敗しました。";
                await UpdateStateSafeAsync(RecordingState.Failed, errorMessage);
                return new RecordingResult(false, recordingId, errorMessage);
            }

            mediaPath = await storage.CommitAsync(mediaPath, cancellationToken);
            isCommitted = true;
            await repository.UpdateFilePathAsync(recordingId.Value, mediaPath, cancellationToken);
            await UpdateStateSafeAsync(RecordingState.Completed, null);
            await PublishGlobalToastSafeAsync($"{command.ProgramName} の録音が完了しました。", true);

            return new RecordingResult(true, recordingId, null);
        }
        catch (OperationCanceledException)
        {
            logger.ZLogWarning($"録音処理がキャンセルされました。");
            await UpdateStateSafeAsync(RecordingState.Failed, "録音処理がキャンセルされました。");
            return new RecordingResult(false, recordingId, "録音処理がキャンセルされました。");
        }
        catch (DomainException ex)
        {
            logger.ZLogWarning(ex, $"録音処理でドメイン例外が発生しました。");
            await UpdateStateSafeAsync(RecordingState.Failed, ex.UserMessage);
            return new RecordingResult(false, recordingId, ex.UserMessage);
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"録音処理で例外が発生しました。");
            await UpdateStateSafeAsync(RecordingState.Failed, "録音処理で例外が発生しました。");
            return new RecordingResult(false, recordingId, "録音処理で例外が発生しました。");
        }
        finally
        {
            await CleanupTempSafeAsync();
        }
    }
}
