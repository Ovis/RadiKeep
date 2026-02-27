using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Logics.Logics.RecordJobLogic;

/// <summary>
/// 録音予約ジョブの状態初期化・キャンセルを管理する。
/// 実行自体は BackgroundService 側で行う。
/// </summary>
public class RecordJobLobLogic(
    ILogger<RecordJobLobLogic> logger,
    IAppConfigurationService appConfig,
    IServiceScopeFactory? serviceScopeFactory = null)
{
    private static readonly TimeSpan PreparingLeadTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 録音予約のジョブをスケジュール可能状態へ初期化する。
    /// </summary>
    /// <param name="job">登録対象ジョブ</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobAsync(ScheduleJob job)
    {
        try
        {
            if (serviceScopeFactory == null)
            {
                return (true, null);
            }

            var fireAtUtc = ResolveFireAtUtc(job);
            var prepareStartUtc = fireAtUtc - PreparingLeadTime;

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RadioDbContext>();

            // 既存行を Pending に戻し、UTC 基準の実行時刻を再計算する。
            await dbContext.ScheduleJob
                .Where(x => x.Id == job.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PrepareStartUtc, prepareStartUtc)
                    .SetProperty(x => x.State, ScheduleJobState.Pending)
                    .SetProperty(x => x.QueuedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.ActualStartUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.CompletedUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastErrorCode, ScheduleJobErrorCode.None)
                    .SetProperty(x => x.LastErrorDetail, (string?)null));

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"録音予約ジョブ初期化処理で失敗");
            return (false, ex);
        }
    }

    /// <summary>
    /// 録音予約のジョブを複数初期化する。
    /// </summary>
    /// <param name="jobs">登録対象ジョブ一覧</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> SetScheduleJobsAsync(List<ScheduleJob> jobs)
    {
        foreach (var job in jobs)
        {
            var (isSuccess, error) = await SetScheduleJobAsync(job);
            if (!isSuccess)
            {
                return (false, error);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// スケジュール済み録音予約のジョブ実行をキャンセルする。
    /// </summary>
    /// <param name="jobId">ジョブID</param>
    public ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobAsync(Ulid jobId)
    {
        try
        {
            RecordingCancellationRegistry.Cancel(jobId.ToString());
            return ValueTask.FromResult<(bool IsSuccess, Exception? Error)>((true, null));
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"録音予約ジョブキャンセル処理で失敗");
            return ValueTask.FromResult<(bool IsSuccess, Exception? Error)>((false, ex));
        }
    }

    /// <summary>
    /// 録音予約ジョブを複数キャンセルする。
    /// </summary>
    /// <param name="jobs">削除対象ジョブ一覧</param>
    public async ValueTask<(bool IsSuccess, Exception? Error)> DeleteScheduleJobsAsync(List<ScheduleJob> jobs)
    {
        foreach (var job in jobs)
        {
            var (isSuccess, error) = await DeleteScheduleJobAsync(job.Id);
            if (!isSuccess)
            {
                return (false, error);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 録音種別に応じて実行開始時刻を UTC で算出する。
    /// </summary>
    private DateTimeOffset ResolveFireAtUtc(ScheduleJob job)
    {
        var startDelaySeconds = job.StartDelay?.TotalSeconds ?? appConfig.RecordStartDuration.TotalSeconds;
        return job.RecordingType switch
        {
            RecordingType.TimeFree => job.EndDateTime.AddMinutes(3).ToUniversalTime(),
            RecordingType.OnDemand => DateTimeOffset.UtcNow,
            RecordingType.Immediate => DateTimeOffset.UtcNow,
            RecordingType.RealTime => job.StartDateTime.AddSeconds(-startDelaySeconds).AddSeconds(-1).ToUniversalTime(),
            _ => throw new DomainException("録音タイプが不正です。")
        };
    }
}
