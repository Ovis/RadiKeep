using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Domain.ProgramSchedule;
using RadiKeep.Logics.Interfaces;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.RecordJobLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;
using ZLogger;

namespace RadiKeep.Logics.Logics.ProgramScheduleLogic
{
    public partial class ProgramScheduleLobLogic(
        ILogger<ProgramScheduleLobLogic> logger,
        IRadioAppContext appContext,
        IRadikoApiClient radikoApiClient,
        IRadiruApiClient radiruApiClient,
        IProgramScheduleRepository programScheduleRepository,
        RecordJobLobLogic recordJobLobLogic,
        IEntryMapper entryMapper,
        NotificationLobLogic? notificationLobLogic = null)
    {
        /// <summary>
        /// 指定されたIDの番組情報を取得
        /// </summary>
        /// <param name="programId"></param>
        /// <param name="serviceKind"></param>
        /// <returns></returns>
        public async ValueTask<RadioProgramEntry?> GetProgramAsync(string programId, RadioServiceKind serviceKind)
        {
            RadioProgramEntry? program = serviceKind switch
            {
                RadioServiceKind.Radiko => await GetRadikoProgramAsync(programId),
                RadioServiceKind.Radiru => await GetRadiruProgramAsync(programId),
                _ => null
            };

            return program;
        }

        /// <summary>
        /// 番組表更新が24時間以内に行われたかを確認
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> HasProgramScheduleBeenUpdatedWithin24Hours()
        {
            var lastUpdated = await programScheduleRepository.GetLastUpdatedProgramAsync();

            if (lastUpdated == null)
            {
                return false;
            }

            return lastUpdated.Value.UtcDateTime.UtcToJst().AddHours(24) > appContext.StandardDateTimeOffset;
        }

        /// <summary>
        /// 番組表更新即時実行ジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> ScheduleImmediateUpdateProgramJobAsync()
        {
            var (isSuccess, error) = await recordJobLobLogic.SetImmediateUpdateProgramListAsync();

            if (!isSuccess)
            {
                logger.ZLogError(error, $"番組表更新ジョブのスケジュールに失敗しました。");
                return (false, new DomainException("番組表更新の指示に失敗しました。"));
            }

            return (true, null);
        }


        /// <summary>
        /// 毎日午前8時に番組表更新ジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask ScheduleDailyUpdateProgramJobAsync()
        {
            var (isSuccess, error) = await recordJobLobLogic.SetDailyUpdateProgramJobAsync();

            if (!isSuccess)
            {
                logger.ZLogError(error, $"番組表更新ジョブのスケジュールに失敗しました。");
            }
        }

        /// <summary>
        /// 毎日実行するメンテナンスクリーンアップジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask ScheduleDailyMaintenanceCleanupJobAsync()
        {
            var (isSuccess, error) = await recordJobLobLogic.SetDailyMaintenanceCleanupJobAsync();

            if (!isSuccess)
            {
                logger.ZLogError(error, $"メンテナンスクリーンアップジョブのスケジュールに失敗しました。");
            }
        }

        /// <summary>
        /// ストレージ空き容量監視ジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask ScheduleStorageCapacityMonitorJobAsync()
        {
            var (isSuccess, error) = await recordJobLobLogic.SetStorageCapacityMonitorJobAsync();

            if (!isSuccess)
            {
                logger.ZLogError(error, $"ストレージ空き容量監視ジョブのスケジュールに失敗しました。");
            }
        }

        /// <summary>
        /// 新しいリリース確認ジョブをスケジュール
        /// </summary>
        public async ValueTask ScheduleReleaseCheckJobAsync()
        {
            var (isSuccess, error) = await recordJobLobLogic.SetReleaseCheckJobAsync();

            if (!isSuccess)
            {
                logger.ZLogError(error, $"新しいリリース確認ジョブのスケジュールに失敗しました。");
            }
        }

        /// <summary>
        /// 類似録音抽出ジョブをスケジュール
        /// </summary>
        public async ValueTask ScheduleDuplicateDetectionJobAsync(
            bool enabled,
            int dayOfWeek,
            int hour,
            int minute)
        {
            var (isSuccess, error) = await recordJobLobLogic.SetDuplicateDetectionJobAsync(enabled, dayOfWeek, hour, minute);
            if (!isSuccess)
            {
                logger.ZLogError(error, $"類似録音抽出ジョブのスケジュールに失敗しました。");
            }
        }


        /// <summary>
        /// DBに保存された録音スケジュールをもとにスケジュールジョブを作成
        /// </summary>
        /// <returns></returns>
        public async ValueTask SetScheduleJobFromDbAsync()
        {
            var scheduleJobs = await programScheduleRepository.GetScheduleJobsAsync();

            if (scheduleJobs.Count == 0)
            {
                return;
            }

            var failedRestoreJobIds = new List<Ulid>();
            var disabledJobIds = new List<Ulid>();
            var disableFailedJobIds = new List<Ulid>();

            foreach (var scheduleJob in scheduleJobs.Where(x => x.IsEnabled))
            {
                var (isSuccess, error) = await recordJobLobLogic.SetScheduleJobAsync(scheduleJob);
                if (isSuccess)
                {
                    continue;
                }

                failedRestoreJobIds.Add(scheduleJob.Id);
                logger.ZLogError(error, $"起動時の録音ジョブ復元に失敗しました。 jobId={scheduleJob.Id}");

                try
                {
                    var disabled = await programScheduleRepository.DisableScheduleJobAsync(scheduleJob.Id);
                    if (disabled)
                    {
                        disabledJobIds.Add(scheduleJob.Id);
                    }
                    else
                    {
                        disableFailedJobIds.Add(scheduleJob.Id);
                        logger.ZLogWarning($"復元失敗ジョブの無効化対象が見つかりませんでした。 jobId={scheduleJob.Id}");
                    }
                }
                catch (Exception disableEx)
                {
                    disableFailedJobIds.Add(scheduleJob.Id);
                    logger.ZLogError(disableEx, $"復元失敗ジョブの無効化に失敗しました。 jobId={scheduleJob.Id}");
                }
            }

            if (failedRestoreJobIds.Count > 0)
            {
                logger.ZLogWarning(
                    $"起動時ジョブ復元で失敗: {failedRestoreJobIds.Count}件, 無効化成功: {disabledJobIds.Count}件, 無効化失敗: {disableFailedJobIds.Count}件");

                if (notificationLobLogic != null)
                {
                    var failedRestoreIds = string.Join(", ", failedRestoreJobIds.Take(10));
                    var failedRestoreSuffix = failedRestoreJobIds.Count > 10 ? " ..." : string.Empty;
                    var disabledIds = string.Join(", ", disabledJobIds.Take(10));
                    var disabledSuffix = disabledJobIds.Count > 10 ? " ..." : string.Empty;
                    var disableFailedIds = string.Join(", ", disableFailedJobIds.Take(10));
                    var disableFailedSuffix = disableFailedJobIds.Count > 10 ? " ..." : string.Empty;
                    await notificationLobLogic.SetNotificationAsync(
                        logLevel: LogLevel.Warning,
                        category: NoticeCategory.SystemError,
                        message:
                        $"起動時ジョブ復元で失敗: {failedRestoreJobIds.Count}件, 無効化成功: {disabledJobIds.Count}件, 無効化失敗: {disableFailedJobIds.Count}件。" +
                        $"復元失敗jobId={failedRestoreIds}{failedRestoreSuffix}; " +
                        $"無効化成功jobId={disabledIds}{disabledSuffix}; " +
                        $"無効化失敗jobId={disableFailedIds}{disableFailedSuffix}");
                }
            }
        }





        /// <summary>
        /// 番組表の最終更新日時を保存
        /// </summary>
        /// <returns></returns>
        public async ValueTask SetProgramLastUpdateDateTimeAsync()
        {
            try
            {
                await programScheduleRepository.SetLastUpdatedProgramAsync(appContext.StandardDateTimeOffset);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"番組表更新日時の更新に失敗");
                throw;
            }
        }
    }
}

