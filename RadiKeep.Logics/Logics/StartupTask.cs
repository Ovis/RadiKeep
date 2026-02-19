using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Logics.Logics
{
    public class StartupTask(
        ILogger<StartupTask> logger,
        IAppConfigurationService config,
        IFfmpegService ffmpegService,
        RadikoUniqueProcessLogic radikoLogic,
        ProgramScheduleLobLogic programScheduleLogic,
        LogMaintenanceLobLogic logMaintenanceLobLogic,
        TemporaryStorageMaintenanceLobLogic temporaryStorageMaintenanceLobLogic,
        StorageCapacityMonitorLobLogic storageCapacityMonitorLobLogic,
        StationLobLogic stationLobLogic,
        NotificationLobLogic notificationLobLogic)
    {
        public async Task InitializeAsync()
        {
            try
            {
                // FFmpeg調整
                if (!ffmpegService.Initialize())
                {
                    await notificationLobLogic.SetNotificationAsync(
                        logLevel: LogLevel.Error,
                        category: NoticeCategory.SystemError,
                        message: "起動処理でエラーが発生しました。ffmpegがインストールされていません。"
                    );

                    throw new DomainException("FFmpegがインストールされていません。");
                }

                // radikoログイン処理 
                {
                    await radikoLogic.LoginRadikoAsync();
                }

                // 放送局情報の初期化
                {
                    // radiko
                    {
                        if (!(await stationLobLogic.CheckInitializedRadikoStationAsync()))
                        {
                            // radikoの放送局情報を初期化
                            await stationLobLogic.UpsertRadikoStationDefinitionAsync();
                        }

                        // radikoの放送局データをキャッシュ
                        var radikoStationList = await stationLobLogic.GetAllRadikoStationAsync();

                        config.UpdateRadikoStationDic(radikoStationList);
                    }


                    // らじる★らじる
                    {
                        if (!await stationLobLogic.CheckInitializedRadiruRadiruStationAsync())
                        {
                            // らじる★らじるの放送局情報を初期化
                            await stationLobLogic.UpdateRadiruStationInformationAsync();
                        }
                    }
                }


                // Quartzのジョブ情報をDBから取得して設定
                {
                    await programScheduleLogic.SetScheduleJobFromDbAsync();
                }

                // 番組表更新関係
                {
                    // 番組表更新ジョブのスケジュール
                    await programScheduleLogic.ScheduleDailyUpdateProgramJobAsync();
                    await programScheduleLogic.ScheduleDailyMaintenanceCleanupJobAsync();
                    await programScheduleLogic.ScheduleStorageCapacityMonitorJobAsync();
                    await programScheduleLogic.ScheduleReleaseCheckJobAsync();
                    await programScheduleLogic.ScheduleDuplicateDetectionJobAsync(
                        enabled: config.DuplicateDetectionIntervalDays > 0,
                        dayOfWeek: config.DuplicateDetectionScheduleDayOfWeek,
                        hour: config.DuplicateDetectionScheduleHour,
                        minute: config.DuplicateDetectionScheduleMinute);

                    // 24時間以内に番組表更新が行われていない場合、即時更新ジョブをスケジュール
                    if (await programScheduleLogic.HasProgramScheduleBeenUpdatedWithin24Hours() is false)
                    {
                        await programScheduleLogic.ScheduleImmediateUpdateProgramJobAsync();
                    }
                }

                // ログメンテナンスを起動時に1回実行
                await logMaintenanceLobLogic.CleanupOldLogFilesAsync(config.LogRetentionDays);
                await temporaryStorageMaintenanceLobLogic.CleanupAsync();

                // ストレージ空き容量監視を起動時に1回実行
                await storageCapacityMonitorLobLogic.CheckAndNotifyLowDiskSpaceAsync();
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"起動処理でエラー発生");

                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Error,
                    category: NoticeCategory.SystemError,
                    message: "起動処理でエラーが発生しました。ログを確認してください。"
                );

                throw;
            }
        }
    }
}
