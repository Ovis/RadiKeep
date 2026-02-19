using Microsoft.Extensions.Logging;
using Quartz;
using RadiKeep.Logics.Logics;
using ZLogger;

namespace RadiKeep.Logics.Jobs;

/// <summary>
/// 保存先ストレージの空き容量を定期監視するジョブ。
/// </summary>
[DisallowConcurrentExecution]
public class StorageCapacityMonitorJob(
    ILogger<StorageCapacityMonitorJob> logger,
    StorageCapacityMonitorLobLogic storageCapacityMonitorLobLogic) : IJob
{
    /// <summary>
    /// ジョブ本体。
    /// </summary>
    /// <param name="context">Quartzジョブ実行コンテキスト</param>
    public async Task Execute(IJobExecutionContext context)
    {
        logger.ZLogDebug($"ストレージ空き容量監視ジョブを実行します。");
        await storageCapacityMonitorLobLogic.CheckAndNotifyLowDiskSpaceAsync(context.CancellationToken);
    }
}

