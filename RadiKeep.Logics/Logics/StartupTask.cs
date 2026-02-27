using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
        NotificationLobLogic notificationLobLogic,
        IServiceScopeFactory? serviceScopeFactory = null)
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

                // 番組表更新関係
                {
                    // 24時間以内に番組表更新が行われていない場合のみ、起動時に即時更新を実行する。
                    if (await programScheduleLogic.HasProgramScheduleBeenUpdatedWithin24Hours() is false)
                    {
                        if (serviceScopeFactory != null)
                        {
                            // 起動をブロックしないため、更新はバックグラウンドで開始する。
                            // 専用スコープを作成して破棄済み DbContext 参照を防ぐ。
                            _ = Task.Run(
                                async () =>
                                {
                                    try
                                    {
                                        using var scope = serviceScopeFactory.CreateScope();
                                        var scopedRunner = scope.ServiceProvider.GetRequiredService<ProgramUpdateRunner>();
                                        await scopedRunner.ExecuteAsync("startup");
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.ZLogError(ex, $"起動時の番組表更新バックグラウンド実行でエラーが発生しました。");
                                    }
                                });
                        }
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
