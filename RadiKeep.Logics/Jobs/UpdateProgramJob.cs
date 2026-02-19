using Microsoft.Extensions.Logging;
using Quartz;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.ReserveLogic;
using RadiKeep.Logics.Logics.StationLogic;
using ZLogger;

namespace RadiKeep.Logics.Jobs
{
    public class UpdateProgramJob(
        ILogger<UpdateProgramJob> logger,
        StationLobLogic stationLobLogic,
        RadikoUniqueProcessLogic radikoUniqueProcessLogic,
        ReserveLobLogic reserveLobLogic,
        ProgramScheduleLobLogic programScheduleLobLogic,
        NotificationLobLogic notificationLobLogic) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.ZLogInformation($"UpdateProgramJob is running.");

            try
            {
                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Information,
                    category: NoticeCategory.UpdateProgramStart,
                    message: "番組表の更新を開始します。"
                );

                // radiko
                {
                    // 番組表更新時はエリア情報を必ず再判定する。
                    await radikoUniqueProcessLogic.RefreshRadikoAreaCacheAsync();

                    if (!await stationLobLogic.CheckInitializedRadikoStationAsync())
                    {
                        // radikoの放送局情報を初期化
                        await stationLobLogic.UpsertRadikoStationDefinitionAsync();
                    }

                    // radikoの番組データを更新
                    await programScheduleLobLogic.UpdateLatestRadikoProgramDataAsync();

                    // 古い番組表情報を削除
                    await programScheduleLobLogic.DeleteOldRadikoProgramAsync();
                }

                // らじる★らじる
                {
                    if (!(await stationLobLogic.CheckInitializedRadiruRadiruStationAsync()))
                    {
                        // らじる★らじるの放送局情報を初期化
                        await stationLobLogic.UpdateRadiruStationInformationAsync();
                    }

                    // NHKラジオの番組データを更新
                    await programScheduleLobLogic.UpdateRadiruProgramDataAsync();

                    // 古い番組表情報を削除
                    await programScheduleLobLogic.DeleteOldRadiruProgramAsync();
                }

                // 古い予約情報を削除
                await reserveLobLogic.DeleteOldReserveEntryAsync();

                // キーワード予約の登録データをもとに予約を生成
                await reserveLobLogic.SetAllKeywordReserveScheduleAsync();

                // 番組表の最終更新日時を更新
                await programScheduleLobLogic.SetProgramLastUpdateDateTimeAsync();

                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Information,
                    category: NoticeCategory.UpdateProgramEnd,
                    message: "番組表の更新が完了しました。"
                );
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"番組表の更新に失敗しました。");

                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Error,
                    category: NoticeCategory.UpdateProgramError,
                    message: "番組表の更新に失敗しました。"
                );
            }
        }
    }
}
