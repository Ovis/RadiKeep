using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.ReserveLogic;
using RadiKeep.Logics.Logics.StationLogic;
using ZLogger;

namespace RadiKeep.Logics.Logics.ProgramScheduleLogic;

/// <summary>
/// 番組表更新処理を実行するランナー。
/// </summary>
public class ProgramUpdateRunner(
    ILogger<ProgramUpdateRunner> logger,
    StationLobLogic stationLobLogic,
    RadikoUniqueProcessLogic radikoUniqueProcessLogic,
    ReserveLobLogic reserveLobLogic,
    ProgramScheduleLobLogic programScheduleLobLogic,
    NotificationLobLogic notificationLobLogic)
{
    private static readonly SemaphoreSlim ExecutionGate = new(1, 1);

    /// <summary>
    /// 番組表更新処理を実行する。
    /// </summary>
    /// <param name="triggerSource">起動元識別子</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    public async ValueTask ExecuteAsync(string triggerSource, CancellationToken cancellationToken = default)
    {
        if (!await ExecutionGate.WaitAsync(0, cancellationToken))
        {
            logger.ZLogInformation($"番組表更新は既に実行中のためスキップしました。 source={triggerSource}");
            return;
        }

        try
        {
            logger.ZLogInformation($"番組表更新を開始します。 source={triggerSource}");
            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Information,
                category: NoticeCategory.UpdateProgramStart,
                message: "番組表の更新を開始します。");

            // radiko 側の放送局/番組情報を更新する。
            await radikoUniqueProcessLogic.RefreshRadikoAreaCacheAsync();
            if (!await stationLobLogic.CheckInitializedRadikoStationAsync())
            {
                await stationLobLogic.UpsertRadikoStationDefinitionAsync();
            }
            await programScheduleLobLogic.UpdateLatestRadikoProgramDataAsync();
            await programScheduleLobLogic.DeleteOldRadikoProgramAsync();

            // らじる★らじる側の放送局/番組情報を更新する。
            if (!await stationLobLogic.CheckInitializedRadiruRadiruStationAsync())
            {
                await stationLobLogic.UpdateRadiruStationInformationAsync();
            }
            await programScheduleLobLogic.UpdateRadiruProgramDataAsync();
            await programScheduleLobLogic.DeleteOldRadiruProgramAsync();

            // 更新後に予約再生成と更新時刻記録を行う。
            await reserveLobLogic.DeleteOldReserveEntryAsync();
            await reserveLobLogic.SetAllKeywordReserveScheduleAsync();
            await programScheduleLobLogic.SetProgramLastUpdateDateTimeAsync();

            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Information,
                category: NoticeCategory.UpdateProgramEnd,
                message: "番組表の更新が完了しました。");
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"番組表の更新に失敗しました。 source={triggerSource}");
            await notificationLobLogic.SetNotificationAsync(
                logLevel: LogLevel.Error,
                category: NoticeCategory.UpdateProgramError,
                message: "番組表の更新に失敗しました。");
        }
        finally
        {
            ExecutionGate.Release();
        }
    }
}
