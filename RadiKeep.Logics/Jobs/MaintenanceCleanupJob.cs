using Quartz;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Logics;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Logics.Jobs;

/// <summary>
/// 日次メンテナンスとしてログ・一時保存領域をクリーンアップするジョブ。
/// </summary>
[DisallowConcurrentExecution]
public class MaintenanceCleanupJob(
    ILogger<MaintenanceCleanupJob> logger,
    LogMaintenanceLobLogic logMaintenanceLobLogic,
    TemporaryStorageMaintenanceLobLogic temporaryStorageMaintenanceLobLogic,
    IAppConfigurationService appConfigurationService) : IJob
{
    /// <summary>
    /// ジョブ本体。
    /// </summary>
    /// <param name="context">Quartzジョブ実行コンテキスト</param>
    public async Task Execute(IJobExecutionContext context)
    {
        var retentionDays = appConfigurationService.LogRetentionDays;
        logger.ZLogInformation($"ログメンテナンスを開始します。保持日数={retentionDays}");
        await logMaintenanceLobLogic.CleanupOldLogFilesAsync(retentionDays, context.CancellationToken);
        await temporaryStorageMaintenanceLobLogic.CleanupAsync(context.CancellationToken);
    }
}
