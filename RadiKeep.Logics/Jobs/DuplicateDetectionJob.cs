using Quartz;
using RadiKeep.Logics.Logics.RecordedRadioLogic;

namespace RadiKeep.Logics.Jobs;

/// <summary>
/// 類似録音抽出ジョブ
/// </summary>
public class DuplicateDetectionJob(RecordedDuplicateDetectionLobLogic duplicateDetectionLobLogic) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var source = context.MergedJobDataMap.GetString("triggerSource") ?? "scheduled";
        var lookbackDays = context.MergedJobDataMap.GetInt("lookbackDays");
        var maxPhase1Groups = context.MergedJobDataMap.GetInt("maxPhase1Groups");
        var phase2Mode = context.MergedJobDataMap.GetString("phase2Mode") ?? "light";
        var broadcastClusterWindowHours = context.MergedJobDataMap.GetInt("broadcastClusterWindowHours");
        await duplicateDetectionLobLogic.ExecuteAsync(
            source,
            lookbackDays,
            maxPhase1Groups,
            phase2Mode,
            broadcastClusterWindowHours,
            context.CancellationToken);
    }
}
