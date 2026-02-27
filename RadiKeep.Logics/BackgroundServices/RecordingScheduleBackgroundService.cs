using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using ZLogger;

namespace RadiKeep.Logics.BackgroundServices;

/// <summary>
/// 録音予約ジョブの起動時復元を担当するバックグラウンドサービス。
/// </summary>
public class RecordingScheduleBackgroundService(
    ILogger<RecordingScheduleBackgroundService> logger,
    IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 録音予約の復元処理を実行する。
    /// 初回に失敗した場合は一定間隔で再試行する。
    /// </summary>
    /// <param name="stoppingToken">停止トークン</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.ZLogInformation($"録音スケジューラサービスを開始しました。");

        var restored = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!restored)
            {
                restored = await TryRestoreScheduleJobsAsync(stoppingToken);

                if (!restored)
                {
                    await Task.Delay(RetryInterval, stoppingToken);
                    continue;
                }
            }

            // 復元完了後は HostedService として待機し、停止要求のみ監視する。
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }

        logger.ZLogInformation($"録音スケジューラサービスを終了しました。");
    }

    /// <summary>
    /// DB から録音予約を読み込み、録音ジョブへ復元登録する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>復元が成功したか</returns>
    private async ValueTask<bool> TryRestoreScheduleJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var programScheduleLobLogic = scope.ServiceProvider.GetRequiredService<ProgramScheduleLobLogic>();
            await programScheduleLobLogic.SetScheduleJobFromDbAsync();
            logger.ZLogInformation($"録音予約ジョブの復元が完了しました。");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"録音予約ジョブ復元に失敗したため再試行します。");
            return false;
        }
    }
}
