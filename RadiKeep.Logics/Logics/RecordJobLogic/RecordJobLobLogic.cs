using Microsoft.Extensions.Logging;
using Quartz;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Jobs;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Logics.Logics.RecordJobLogic
{
    /// <summary>
    /// Quartzによるジョブの作成、削除の統括ロジック
    /// </summary>
    public class RecordJobLobLogic(
        ILogger<RecordJobLobLogic> logger,
        ISchedulerFactory schedulerFactory,
        IAppConfigurationService appConfig,
        IRadioAppContext context)
    {
        private const string ScheduleJobGroup = "RadioScheduleJobGroup";
        private const string ScheduleTriggerGroup = "RadioScheduleTriggerGroup";
        private const string ProgramUpdateJobGroup = "RadioProgramUpdateJobGroup";
        private const string ProgramUpdateTriggerGroup = "RadioProgramUpdateTriggerGroup";
        private const string MaintenanceCleanupJobGroup = "RadioMaintenanceCleanupJobGroup";
        private const string MaintenanceCleanupTriggerGroup = "RadioMaintenanceCleanupTriggerGroup";
        private const string StorageMonitorJobGroup = "RadioStorageMonitorJobGroup";
        private const string StorageMonitorTriggerGroup = "RadioStorageMonitorTriggerGroup";
        private const string ReleaseCheckJobGroup = "ReleaseCheckJobGroup";
        private const string ReleaseCheckTriggerGroup = "ReleaseCheckTriggerGroup";
        private const string DuplicateDetectionJobGroup = "DuplicateDetectionJobGroup";
        private const string DuplicateDetectionTriggerGroup = "DuplicateDetectionTriggerGroup";

        /// <summary>
        /// 録音予約のジョブをスケジュール
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobAsync(ScheduleJob job)
        {
            // スケジューラのインスタンスを取得
            var scheduler = await schedulerFactory.GetScheduler();

            return await SetScheduleJobInternalAsync(scheduler, job);
        }


        /// <summary>
        /// 録音予約のジョブをスケジュール
        /// </summary>
        /// <param name="jobs"></param>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobsAsync(List<ScheduleJob> jobs)
        {
            // スケジューラのインスタンスを取得
            var scheduler = await schedulerFactory.GetScheduler();

            foreach (var job in jobs)
            {
                var (isSuccess, error) = await SetScheduleJobInternalAsync(scheduler, job);

                if (!isSuccess)
                {
                    return (false, error);
                }
            }

            return (true, null);
        }


        /// <summary>
        /// 録音予約のジョブをスケジュール
        /// </summary>
        /// <param name="scheduler"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        private async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobInternalAsync(
            IScheduler scheduler,
            ScheduleJob job)
        {
            try
            {
                await RemoveExistingJobAsync(scheduler, new JobKey($"{job.Id}", ScheduleJobGroup));

                // ジョブの詳細を定義
                var jobDetail = JobBuilder.Create<RadioRecJob>()
                    .UsingJobData("isTimeFree", job.RecordingType == RecordingType.TimeFree)
                    .UsingJobData("isOnDemand", job.RecordingType == RecordingType.OnDemand)
                    .UsingJobData("programId", job.ProgramId)
                    .UsingJobData("serviceKind", job.ServiceKind.ToString())
                    .UsingJobData("scheduleJobId", job.Id.ToString())
                    .UsingJobData("programName", job.Title)
                    .UsingJobData("startDelay", job.StartDelay?.TotalSeconds ?? appConfig.RecordStartDuration.TotalSeconds)
                    .UsingJobData("endDelay", job.EndDelay?.TotalSeconds ?? appConfig.RecordEndDuration.TotalSeconds)
                    .WithIdentity($"{job.Id}", ScheduleJobGroup)
                    .Build();

                // トリガーの詳細を定義
                var triggerBuilder = TriggerBuilder.Create()
                    .WithIdentity($"{job.Id}", ScheduleTriggerGroup);

                var trigger = job.RecordingType switch
                {
                    RecordingType.TimeFree =>
                        triggerBuilder.StartAt(job.EndDateTime.AddMinutes(3)).Build(),
                    RecordingType.OnDemand =>
                        triggerBuilder.StartNow().Build(),
                    RecordingType.Immediate =>
                        triggerBuilder.StartNow().Build(),
                    RecordingType.RealTime =>
                        // ディレイで指定された時間分+1秒(録音前の処理にかかる時間の猶予として)前にジョブを実行する
                        triggerBuilder
                        .StartAt(job.StartDateTime.AddSeconds(-(job.StartDelay?.TotalSeconds ?? appConfig.RecordStartDuration.TotalSeconds)).AddSeconds(-1))
                        .Build(),
                    _ => throw new DomainException("録音タイプが不正です。")
                };

                // ジョブをスケジュール
                await scheduler.ScheduleJob(jobDetail, trigger);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"録音予約ジョブスケジュール処理で失敗");
                return (false, e);
            }
        }





        /// <summary>
        /// スケジュールされた録音予約のジョブを削除
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobAsync(Ulid jobId)
        {
            // スケジューラのインスタンスを取得
            var scheduler = await schedulerFactory.GetScheduler();

            return await DeleteScheduleJobInternalAsync(scheduler, jobId);
        }


        /// <summary>
        /// 録音予約のジョブリストをもとにスケジュールジョブを削除
        /// </summary>
        /// <param name="jobs"></param>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobsAsync(List<ScheduleJob> jobs)
        {
            // スケジューラのインスタンスを取得
            var scheduler = await schedulerFactory.GetScheduler();

            foreach (var job in jobs)
            {
                var (isSuccess, error) = await DeleteScheduleJobInternalAsync(scheduler, job.Id);

                if (!isSuccess)
                {
                    return (false, error);
                }
            }

            return (true, null);
        }



        /// <summary>
        /// スケジュール済録音予約のジョブを削除
        /// </summary>
        /// <param name="scheduler"></param>
        /// <param name="jobId"></param>
        /// <returns></returns>
        private async ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobInternalAsync(IScheduler scheduler, Ulid jobId)
        {
            try
            {
                var jobKey = new JobKey(jobId.ToString(), ScheduleJobGroup);
                RecordingCancellationRegistry.Cancel(jobId.ToString());
                await scheduler.DeleteJob(jobKey);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"録音予約ジョブスケジュール削除処理で失敗");
                return (false, e);
            }
        }




        /// <summary>
        /// 番組表更新即時実行ジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetImmediateUpdateProgramListAsync()
        {
            try
            {
                // スケジューラのインスタンスを取得
                var scheduler = await schedulerFactory.GetScheduler();

                var jobKey = new JobKey("ImmediateUpdateProgramJob", ProgramUpdateJobGroup);
                if (await scheduler.CheckExists(jobKey))
                {
                    return (true, null);
                }

                // ジョブの詳細を定義
                var job = JobBuilder.Create<UpdateProgramJob>()
                    .WithIdentity(jobKey)
                    .Build();

                // トリガーの詳細を定義
                var trigger = TriggerBuilder.Create()
                    .WithIdentity("ImmediateUpdateProgramTrigger", ProgramUpdateTriggerGroup)
                    .StartNow()
                    .Build();

                // ジョブをスケジュール
                await scheduler.ScheduleJob(job, trigger);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"番組表更新即時実行ジョブスケジュール処理で失敗");
                return (false, e);
            }
        }


        /// <summary>
        /// 毎日午前8時に番組表更新ジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetDailyUpdateProgramJobAsync()
        {
            try
            {
                // スケジューラのインスタンスを取得
                var scheduler = await schedulerFactory.GetScheduler();

                var jobKey = new JobKey("DailyUpdateProgramJob", ProgramUpdateJobGroup);
                await RemoveExistingJobAsync(scheduler, jobKey);

                // ジョブの詳細を定義
                var job = JobBuilder.Create<UpdateProgramJob>()
                    .WithIdentity(jobKey)
                    .Build();

                // Quartzで毎日午前8時にジョブを実行するトリガーを作成
                var trigger = TriggerBuilder.Create()
                    .WithIdentity("DailyUpdateProgramTrigger", ProgramUpdateTriggerGroup)
                    .WithDailyTimeIntervalSchedule(x => x
                        .OnEveryDay()
                        .InTimeZone(context.TimeZoneInfo)
                        .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(8, 0))
                        .WithIntervalInHours(24))
                    .Build();

                // ジョブをスケジュール
                await scheduler.ScheduleJob(job, trigger);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"番組表更新定期ジョブスケジュール処理で失敗");
                return (false, e);
            }
        }

        /// <summary>
        /// 毎日午前4時にメンテナンスクリーンアップジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetDailyMaintenanceCleanupJobAsync()
        {
            try
            {
                var scheduler = await schedulerFactory.GetScheduler();

                var jobKey = new JobKey("DailyMaintenanceCleanupJob", MaintenanceCleanupJobGroup);
                await RemoveExistingJobAsync(scheduler, jobKey);

                var job = JobBuilder.Create<MaintenanceCleanupJob>()
                    .WithIdentity(jobKey)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity("DailyMaintenanceCleanupTrigger", MaintenanceCleanupTriggerGroup)
                    .WithDailyTimeIntervalSchedule(x => x
                        .OnEveryDay()
                        .InTimeZone(context.TimeZoneInfo)
                        .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(4, 0))
                        .WithIntervalInHours(24))
                    .Build();

                await scheduler.ScheduleJob(job, trigger);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"メンテナンスクリーンアップジョブスケジュール処理で失敗");
                return (false, e);
            }
        }

        /// <summary>
        /// 一定間隔でストレージ空き容量監視ジョブをスケジュール
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetStorageCapacityMonitorJobAsync()
        {
            try
            {
                var scheduler = await schedulerFactory.GetScheduler();

                var jobKey = new JobKey("StorageCapacityMonitorJob", StorageMonitorJobGroup);
                await RemoveExistingJobAsync(scheduler, jobKey);

                var job = JobBuilder.Create<StorageCapacityMonitorJob>()
                    .WithIdentity(jobKey)
                    .Build();

                var intervalMinutes = appConfig.StorageLowSpaceCheckIntervalMinutes > 0
                    ? appConfig.StorageLowSpaceCheckIntervalMinutes
                    : 30;

                var trigger = TriggerBuilder.Create()
                    .WithIdentity("StorageCapacityMonitorTrigger", StorageMonitorTriggerGroup)
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInMinutes(intervalMinutes)
                        .RepeatForever())
                    .Build();

                await scheduler.ScheduleJob(job, trigger);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"ストレージ空き容量監視ジョブスケジュール処理で失敗");
                return (false, e);
            }
        }

        /// <summary>
        /// 新しいリリース確認ジョブをスケジュール
        /// </summary>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetReleaseCheckJobAsync()
        {
            try
            {
                var scheduler = await schedulerFactory.GetScheduler();

                var jobKey = new JobKey("ReleaseCheckJob", ReleaseCheckJobGroup);
                await RemoveExistingJobAsync(scheduler, jobKey);

                var job = JobBuilder.Create<ReleaseCheckJob>()
                    .WithIdentity(jobKey)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity("ReleaseCheckTrigger", ReleaseCheckTriggerGroup)
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInHours(24)
                        .RepeatForever())
                    .Build();

                await scheduler.ScheduleJob(job, trigger);

                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"新しいリリース確認ジョブスケジュール処理で失敗");
                return (false, e);
            }
        }

        /// <summary>
        /// 類似録音抽出ジョブを即時実行でスケジュール
        /// </summary>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetImmediateDuplicateDetectionJobAsync(
            int lookbackDays = 30,
            int maxPhase1Groups = 100,
            string phase2Mode = "light",
            int broadcastClusterWindowHours = 48)
        {
            try
            {
                var scheduler = await schedulerFactory.GetScheduler();

                var jobKey = new JobKey($"ImmediateDuplicateDetectionJob-{Guid.NewGuid():N}", DuplicateDetectionJobGroup);
                var triggerKey = new TriggerKey($"ImmediateDuplicateDetectionTrigger-{Guid.NewGuid():N}", DuplicateDetectionTriggerGroup);

                var job = JobBuilder.Create<DuplicateDetectionJob>()
                    .UsingJobData("triggerSource", "manual")
                    .UsingJobData("lookbackDays", lookbackDays)
                    .UsingJobData("maxPhase1Groups", maxPhase1Groups)
                    .UsingJobData("phase2Mode", phase2Mode)
                    .UsingJobData("broadcastClusterWindowHours", broadcastClusterWindowHours)
                    .WithIdentity(jobKey)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .StartNow()
                    .Build();

                await scheduler.ScheduleJob(job, trigger);
                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"類似録音抽出ジョブの即時実行スケジュールに失敗");
                return (false, e);
            }
        }

        /// <summary>
        /// 類似録音抽出ジョブの定期実行をスケジュール
        /// </summary>
        public async ValueTask<(bool IsSuccess, Exception? Error)> SetDuplicateDetectionJobAsync(
            bool enabled,
            int dayOfWeek,
            int hour,
            int minute)
        {
            try
            {
                var scheduler = await schedulerFactory.GetScheduler();
                var jobKey = new JobKey("DuplicateDetectionJob", DuplicateDetectionJobGroup);
                await RemoveExistingJobAsync(scheduler, jobKey);

                if (!enabled)
                {
                    logger.ZLogInformation($"類似録音抽出の定期実行は無効設定のためスケジュールしません。");
                    return (true, null);
                }

                if (dayOfWeek is < 0 or > 6 || hour is < 0 or > 23 || minute is < 0 or > 59)
                {
                    return (false, new ArgumentOutOfRangeException(nameof(dayOfWeek), "類似録音抽出の実行曜日/時刻が不正です。"));
                }

                var job = JobBuilder.Create<DuplicateDetectionJob>()
                    .UsingJobData("triggerSource", "scheduled")
                    .UsingJobData("lookbackDays", 30)
                    .UsingJobData("maxPhase1Groups", 100)
                    .UsingJobData("phase2Mode", "light")
                    .UsingJobData("broadcastClusterWindowHours", 48)
                    .WithIdentity(jobKey)
                    .Build();

                var quartzDayOfWeek = dayOfWeek switch
                {
                    0 => "SUN",
                    1 => "MON",
                    2 => "TUE",
                    3 => "WED",
                    4 => "THU",
                    5 => "FRI",
                    6 => "SAT",
                    _ => "SUN"
                };

                var cron = $"0 {minute} {hour} ? * {quartzDayOfWeek}";
                var trigger = TriggerBuilder.Create()
                    .WithIdentity("DuplicateDetectionTrigger", DuplicateDetectionTriggerGroup)
                    .WithCronSchedule(cron, x => x.InTimeZone(context.TimeZoneInfo))
                    .Build();

                await scheduler.ScheduleJob(job, trigger);
                return (true, null);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"類似録音抽出ジョブの定期スケジュールに失敗");
                return (false, e);
            }
        }

        /// <summary>
        /// 既存ジョブが存在する場合は削除して冪等性を確保する
        /// </summary>
        /// <param name="scheduler">スケジューラ</param>
        /// <param name="jobKey">削除対象のジョブキー</param>
        private static async ValueTask RemoveExistingJobAsync(IScheduler scheduler, JobKey jobKey)
        {
            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
            }
        }
    }
}
