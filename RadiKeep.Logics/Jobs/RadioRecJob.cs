using Microsoft.Extensions.Logging;
using Quartz;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Models.Enums;
using ZLogger;

namespace RadiKeep.Logics.Jobs
{
    [DisallowConcurrentExecution]
    public class RadioRecJob(
        ILogger<RadioRecJob> logger,
        RecordingLobLogic recordingLogic,
        NotificationLobLogic notificationLobLogic) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            logger.ZLogDebug($"RadioRecJob is running.");

            var programName = string.Empty;
            // ジョブデータマップから引数を取得
            var isTimeFree = context.JobDetail.JobDataMap.GetBoolean("isTimeFree");
            var isOnDemand = context.JobDetail.JobDataMap.GetBoolean("isOnDemand");

            using var interruptCts = new CancellationTokenSource();

            try
            {
                context.JobDetail.JobDataMap.TryGetString("programId", out var programId);
                context.JobDetail.JobDataMap.TryGetString("scheduleJobId", out var scheduleJobId);
                context.JobDetail.JobDataMap.TryGetString("serviceKind", out var serviceKindString);
                context.JobDetail.JobDataMap.TryGetString("programName", out programName);
                context.JobDetail.JobDataMap.TryGetDoubleValue("startDelay", out var startDelay);
                context.JobDetail.JobDataMap.TryGetDoubleValue("endDelay", out var endDelay);
                RecordingCancellationRegistry.Register(scheduleJobId ?? string.Empty, interruptCts);

                logger.ZLogDebug($"{programName} の録音を開始");

                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Information,
                    category: NoticeCategory.RecordingStart,
                    message: $"{programName} の録音を開始します。"
                );

                if (!Enum.TryParse(serviceKindString, out RadioServiceKind serviceKind))
                {
                    logger.ZLogError($"サービス種別の取得に失敗しました。");
                    throw new DomainException("サービス種別の取得に失敗しました。");
                }

                if (string.IsNullOrEmpty(programId))
                {
                    logger.ZLogError($"番組IDが指定されていません。");
                    throw new DomainException("番組IDが指定されていません。");
                }

                var (isSuccess, error) = await recordingLogic.RecordRadioAsync(
                    serviceKind: serviceKind,
                    programId: programId,
                    programName: programName ?? string.Empty,
                    scheduleJobId: scheduleJobId ?? string.Empty,
                    isTimeFree: isTimeFree,
                    isOnDemand: isOnDemand,
                    startDelay: startDelay,
                    endDelay: endDelay,
                    cancellationToken: interruptCts.Token);

                if (!isSuccess)
                {
                    logger.ZLogError(error, $"{programName} の録音に失敗しました。");

                    var detailMessage = error?.Message ?? string.Empty;
                    var isCanceled = detailMessage.Contains("キャンセル");
                    if (isCanceled)
                    {
                        await notificationLobLogic.SetNotificationAsync(
                            logLevel: LogLevel.Warning,
                            category: NoticeCategory.RecordingCancel,
                            message: $"{programName} の録音をキャンセルしました。"
                        );
                    }
                    else
                    {
                        var message = string.IsNullOrWhiteSpace(detailMessage)
                            ? $"{programName} の録音に失敗しました。"
                            : $"{programName} の録音に失敗しました。理由: {detailMessage}";

                        await notificationLobLogic.SetNotificationAsync(
                            logLevel: LogLevel.Information,
                            category: NoticeCategory.RecordingError,
                            message: message
                        );
                    }
                }
                else
                {
                    await notificationLobLogic.SetNotificationAsync(
                        logLevel: LogLevel.Information,
                        category: NoticeCategory.RecordingSuccess,
                        message: $"{programName} の録音が完了しました。"
                    );
                }
            }
            catch (OperationCanceledException)
            {
                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Warning,
                    category: NoticeCategory.RecordingCancel,
                    message: $"{programName} の録音をキャンセルしました。"
                );
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"{programName} の録音に失敗しました。");

                var detailMessage = e.Message;
                var message = string.IsNullOrWhiteSpace(detailMessage)
                    ? $"{programName ?? ""} の録音に失敗しました。"
                    : $"{programName ?? ""} の録音に失敗しました。理由: {detailMessage}";

                await notificationLobLogic.SetNotificationAsync(
                    logLevel: LogLevel.Error,
                    category: NoticeCategory.RecordingError,
                    message: message
                );
            }
            finally
            {
                context.JobDetail.JobDataMap.TryGetString("scheduleJobId", out var scheduleJobId);
                RecordingCancellationRegistry.Unregister(scheduleJobId ?? string.Empty);
            }
        }
    }
}
