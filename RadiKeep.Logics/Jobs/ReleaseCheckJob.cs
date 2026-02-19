using Quartz;
using RadiKeep.Logics.Logics.NotificationLogic;

namespace RadiKeep.Logics.Jobs;

/// <summary>
/// GitHub の新リリース確認ジョブ
/// </summary>
public class ReleaseCheckJob(ReleaseCheckLobLogic releaseCheckLobLogic) : IJob
{
    /// <summary>
    /// ジョブ本体
    /// </summary>
    /// <param name="context">Quartzジョブ実行コンテキスト</param>
    public async Task Execute(IJobExecutionContext context)
    {
        await releaseCheckLobLogic.CheckForNewReleaseAsync();
    }
}
