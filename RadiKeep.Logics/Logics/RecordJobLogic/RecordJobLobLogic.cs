using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Logics.Logics.RecordJobLogic;

/// <summary>
/// 録音予約ジョブの登録・解除を管理する。
/// </summary>
public class RecordJobLobLogic(
    ILogger<RecordJobLobLogic> logger,
    IAppConfigurationService appConfig,
    IServiceScopeFactory? serviceScopeFactory = null)
{
    private static readonly ConcurrentDictionary<Ulid, CancellationTokenSource> PendingJobMap = new();

    /// <summary>
    /// 録音予約のジョブをスケジュールする。
    /// </summary>
    /// <param name="job">登録対象ジョブ</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobAsync(ScheduleJob job)
    {
        try
        {
            var fireAtUtc = ResolveFireAtUtc(job);
            var startDelaySeconds = job.StartDelay?.TotalSeconds ?? appConfig.RecordStartDuration.TotalSeconds;
            var endDelaySeconds = job.EndDelay?.TotalSeconds ?? appConfig.RecordEndDuration.TotalSeconds;

            // 実ジョブは in-process で実行する。
            await DeleteScheduleJobAsync(job.Id);
            ScheduleInProcess(job, fireAtUtc, startDelaySeconds, endDelaySeconds);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"録音予約ジョブスケジュール処理で失敗");
            return (false, ex);
        }
    }

    /// <summary>
    /// 録音予約のジョブを複数スケジュールする。
    /// </summary>
    /// <param name="jobs">登録対象ジョブ一覧</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobsAsync(List<ScheduleJob> jobs)
    {
        foreach (var job in jobs)
        {
            var (isSuccess, error) = await SetScheduleJobAsync(job);
            if (!isSuccess)
            {
                return (false, error);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// スケジュール済み録音予約のジョブを削除する。
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobAsync(Ulid jobId)
    {
        try
        {
            if (PendingJobMap.TryRemove(jobId, out var cts))
            {
                await cts.CancelAsync();
                cts.Dispose();
            }

            RecordingCancellationRegistry.Cancel(jobId.ToString());

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"録音予約ジョブスケジュール削除処理で失敗");
            return (false, ex);
        }
    }

    /// <summary>
    /// 録音予約ジョブを複数削除する。
    /// </summary>
    /// <param name="jobs">削除対象ジョブ一覧</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobsAsync(List<ScheduleJob> jobs)
    {
        foreach (var job in jobs)
        {
            var (isSuccess, error) = await DeleteScheduleJobAsync(job.Id);
            if (!isSuccess)
            {
                return (false, error);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 録音種別に応じて実行開始時刻を算出する。
    /// </summary>
    private DateTimeOffset ResolveFireAtUtc(ScheduleJob job)
    {
        var startDelaySeconds = job.StartDelay?.TotalSeconds ?? appConfig.RecordStartDuration.TotalSeconds;
        return job.RecordingType switch
        {
            RecordingType.TimeFree => job.EndDateTime.AddMinutes(3).ToUniversalTime(),
            RecordingType.OnDemand => DateTimeOffset.UtcNow,
            RecordingType.Immediate => DateTimeOffset.UtcNow,
            RecordingType.RealTime => job.StartDateTime.AddSeconds(-startDelaySeconds).AddSeconds(-1).ToUniversalTime(),
            _ => throw new DomainException("録音タイプが不正です。")
        };
    }

    /// <summary>
    /// in-process で録音ジョブを遅延実行する。
    /// </summary>
    private void ScheduleInProcess(ScheduleJob job, DateTimeOffset fireAtUtc, double startDelaySeconds, double endDelaySeconds)
    {
        if (serviceScopeFactory == null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        PendingJobMap[job.Id] = cts;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    var wait = fireAtUtc - DateTimeOffset.UtcNow;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, cts.Token);
                    }

                    await ExecuteRecordingAsync(job, startDelaySeconds, endDelaySeconds, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 予約削除時はキャンセルされる。
                }
                finally
                {
                    PendingJobMap.TryRemove(job.Id, out _);
                }
            },
            cts.Token);
    }

    /// <summary>
    /// 録音実処理を実行し、通知を送信する。
    /// </summary>
    private async Task ExecuteRecordingAsync(
        ScheduleJob job,
        double startDelaySeconds,
        double endDelaySeconds,
        CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory!.CreateScope();
        var recordingLogic = scope.ServiceProvider.GetRequiredService<RecordingLobLogic>();
        var notificationLobLogic = scope.ServiceProvider.GetRequiredService<NotificationLobLogic>();

        using var interruptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        RecordingCancellationRegistry.Register(job.Id.ToString(), interruptCts);

        try
        {
            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Information,
                category: NoticeCategory.RecordingStart,
                message: $"{job.Title} の録音を開始します。");

            var (isSuccess, error) = await recordingLogic.RecordRadioAsync(
                serviceKind: job.ServiceKind,
                programId: job.ProgramId,
                programName: job.Title,
                scheduleJobId: job.Id.ToString(),
                isTimeFree: job.RecordingType == RecordingType.TimeFree,
                isOnDemand: job.RecordingType == RecordingType.OnDemand,
                startDelay: startDelaySeconds,
                endDelay: endDelaySeconds,
                cancellationToken: interruptCts.Token);

            if (!isSuccess)
            {
                var detailMessage = error?.Message ?? string.Empty;
                if (detailMessage.Contains("キャンセル"))
                {
                    await notificationLobLogic.SetNotificationAsync(
                        logLevel: LogLevel.Warning,
                        category: NoticeCategory.RecordingCancel,
                        message: $"{job.Title} の録音をキャンセルしました。");
                    return;
                }

                var message = string.IsNullOrWhiteSpace(detailMessage)
                    ? $"{job.Title} の録音に失敗しました。"
                    : $"{job.Title} の録音に失敗しました。理由: {detailMessage}";
                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Information,
                    category: NoticeCategory.RecordingError,
                    message: message);
                return;
            }

            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Information,
                category: NoticeCategory.RecordingSuccess,
                message: $"{job.Title} の録音が完了しました。");
        }
        catch (OperationCanceledException)
        {
            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Warning,
                category: NoticeCategory.RecordingCancel,
                message: $"{job.Title} の録音をキャンセルしました。");
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"{job.Title} の録音に失敗しました。");
            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Error,
                category: NoticeCategory.RecordingError,
                message: $"{job.Title} の録音に失敗しました。理由: {ex.Message}");
        }
        finally
        {
            RecordingCancellationRegistry.Unregister(job.Id.ToString());
        }
    }

}
