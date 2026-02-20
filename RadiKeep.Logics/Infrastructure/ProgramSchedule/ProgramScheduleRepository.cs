using Microsoft.EntityFrameworkCore;
using RadiKeep.Logics.Domain.ProgramSchedule;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Infrastructure.ProgramSchedule;

/// <summary>
/// 番組表データの永続化を担うリポジトリ実装
/// </summary>
public class ProgramScheduleRepository(RadioDbContext dbContext) : IProgramScheduleRepository
{
    /// <summary>
    /// 指定時刻に放送中のradiko番組を取得する
    /// </summary>
    public async ValueTask<List<RadikoProgram>> GetRadikoNowOnAirAsync(DateTimeOffset standardDateTimeOffset, CancellationToken cancellationToken = default)
    {
        return await dbContext.RadikoPrograms
            .Where(p => standardDateTimeOffset.UtcDateTime >= p.StartTime && standardDateTimeOffset.UtcDateTime <= p.EndTime)
            .OrderBy(r => r.StartTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 指定時刻に放送中のらじる★らじる番組を取得する
    /// </summary>
    public async ValueTask<List<NhkRadiruProgram>> GetRadiruNowOnAirAsync(DateTimeOffset standardDateTimeOffset, CancellationToken cancellationToken = default)
    {
        return await dbContext.NhkRadiruPrograms
            .Where(p => standardDateTimeOffset.UtcDateTime >= p.StartTime && standardDateTimeOffset.UtcDateTime <= p.EndTime)
            .OrderBy(r => r.StartTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// radiko番組一覧を日付と局で取得する
    /// </summary>
    public async ValueTask<List<RadikoProgram>> GetRadikoProgramsAsync(DateOnly date, string stationId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RadikoPrograms
            .Where(r => r.RadioDate == date)
            .Where(r => r.StationId == stationId)
            .OrderBy(r => r.StartTime)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// radiko番組をIDで取得する
    /// </summary>
    public async ValueTask<RadikoProgram?> GetRadikoProgramByIdAsync(string programId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RadikoPrograms
            .AsNoTracking()
            .Where(r => r.ProgramId == programId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// radiko放送局ID一覧を取得する
    /// </summary>
    public async ValueTask<List<string>> GetRadikoStationIdsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.RadikoStations
            .AsNoTracking()
            .Select(r => r.StationId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// radiko番組を追加する
    /// </summary>
    public async ValueTask AddRadikoProgramsIfMissingAsync(IEnumerable<RadikoProgram> programs, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var program in programs)
            {
                var existingProgram = await dbContext.RadikoPrograms.FindAsync([program.ProgramId], cancellationToken);

                if (existingProgram == null)
                {
                    await dbContext.RadikoPrograms.AddAsync(program, cancellationToken);
                }
                else
                {
                    // 番組表の再取得時に画像URLなどが更新される可能性があるため、上書きする。
                    existingProgram.StartTime = program.StartTime;
                    existingProgram.EndTime = program.EndTime;
                    existingProgram.Title = program.Title;
                    existingProgram.Performer = program.Performer;
                    existingProgram.Description = program.Description;
                    existingProgram.RadioDate = program.RadioDate;
                    existingProgram.DaysOfWeek = program.DaysOfWeek;
                    existingProgram.AvailabilityTimeFree = program.AvailabilityTimeFree;
                    existingProgram.ProgramUrl = program.ProgramUrl;
                    existingProgram.ImageUrl = program.ImageUrl;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 全放送局について、指定日までのradiko番組表データが揃っているかを判定する
    /// </summary>
    public async ValueTask<bool> HasRadikoProgramsForAllStationsThroughAsync(DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        var stationIds = await dbContext.RadikoStations
            .AsNoTracking()
            .Select(r => r.StationId)
            .ToListAsync(cancellationToken);

        if (stationIds.Count == 0)
        {
            return false;
        }

        var maxRadioDateByStation = await dbContext.RadikoPrograms
            .AsNoTracking()
            .Where(r => stationIds.Contains(r.StationId))
            .GroupBy(r => r.StationId)
            .Select(g => new
            {
                StationId = g.Key,
                MaxRadioDate = g.Max(x => x.RadioDate)
            })
            .ToListAsync(cancellationToken);

        var maxDateLookup = maxRadioDateByStation.ToDictionary(x => x.StationId, x => x.MaxRadioDate);

        foreach (var stationId in stationIds)
        {
            if (!maxDateLookup.TryGetValue(stationId, out var maxDate))
            {
                return false;
            }

            if (maxDate < targetDate)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// radiko番組を検索する
    /// </summary>
    public async ValueTask<List<RadikoProgram>> SearchRadikoProgramsAsync(
        ProgramSearchEntity searchEntity,
        DateTimeOffset standardDateTimeOffset,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.RadikoPrograms.AsQueryable();

        if (searchEntity.SelectedRadikoStationIds.Count != 0)
        {
            query = query.Where(p => searchEntity.SelectedRadikoStationIds.Contains(p.StationId));
        }

        if (!string.IsNullOrWhiteSpace(searchEntity.Keyword))
        {
            var keywords = searchEntity.Keyword.ParseKeywords();

            if (searchEntity.SearchTitleOnly)
            {
                query = query.Where(p => keywords.All(keyword => p.Title.Contains(keyword)));
            }
            else
            {
                query = query.Where(
                    p =>
                        keywords.All(keyword =>
                            p.Title.Contains(keyword) ||
                            p.Performer.Contains(keyword) ||
                            p.Description.Contains(keyword))
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(searchEntity.ExcludedKeyword))
        {
            var excludedKeywords = searchEntity.ExcludedKeyword.ParseKeywords();

            if (searchEntity.SearchTitleOnlyExcludedKeyword)
            {
                query = query.Where(p => !excludedKeywords.Any(excluded => p.Title.Contains(excluded)));
            }
            else
            {
                query = query.Where(p =>
                    !excludedKeywords.Any(excluded =>
                        p.Title.Contains(excluded) ||
                        p.Performer.Contains(excluded) ||
                        p.Description.Contains(excluded))
                );
            }
        }

        if (searchEntity.SelectedDaysOfWeek.Count != 0)
        {
            var selectedDays = searchEntity.SelectedDaysOfWeek.Aggregate(DaysOfWeek.None, (acc, day) => acc | day);
            query = query.Where(p => (p.DaysOfWeek & selectedDays) != DaysOfWeek.None);
        }

        var limitRadioDate = standardDateTimeOffset.AddDays(-7).ToRadioDate();

        // DateTimeOffset のSQL比較はSQLiteで期待どおりにならない場合があるため、
        // 終了済み判定はアプリ側で評価する。
        var list = (await query
                .Where(r => r.RadioDate >= limitRadioDate)
                .ToListAsync(cancellationToken))
            .Where(
                r =>
                    (searchEntity.IncludeHistoricalPrograms || r.EndTime >= standardDateTimeOffset) &&
                    r.StartTime.TimeOfDay >= searchEntity.StartTime.ToTimeSpan() &&
                    r.EndTime.TimeOfDay <= searchEntity.EndTime.ToTimeSpan())
            .OrderBy(r => r.StartTime)
            .ToList();

        return list;
    }

    /// <summary>
    /// 古いradiko番組を削除する
    /// </summary>
    public async ValueTask DeleteOldRadikoProgramsAsync(DateOnly deleteDate, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var deletePrograms = await dbContext.RadikoPrograms
                .Where(r => r.RadioDate < deleteDate)
                .ToListAsync(cancellationToken);

            dbContext.RadikoPrograms.RemoveRange(deletePrograms);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// らじる★らじる番組一覧を日付/エリア/局で取得する
    /// </summary>
    public async ValueTask<List<NhkRadiruProgram>> GetRadiruProgramsAsync(DateOnly date, string areaId, string stationId, CancellationToken cancellationToken = default)
    {
        return await dbContext.NhkRadiruPrograms
            .Where(r => r.RadioDate == date)
            .Where(r => r.AreaId == areaId)
            .Where(r => r.StationId == stationId)
            .OrderBy(r => r.StartTime)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// らじる★らじる番組をIDで取得する
    /// </summary>
    public async ValueTask<NhkRadiruProgram?> GetRadiruProgramByIdAsync(string programId, CancellationToken cancellationToken = default)
    {
        return await dbContext.NhkRadiruPrograms.FindAsync([programId], cancellationToken);
    }

    /// <summary>
    /// らじる★らじる番組を追加または更新する
    /// </summary>
    public async ValueTask UpsertRadiruProgramsAsync(IEnumerable<NhkRadiruProgram> programs, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var program in programs)
            {
                var existingProgram = await dbContext.NhkRadiruPrograms
                    .Where(r => r.AreaId == program.AreaId)
                    .Where(r => r.StationId == program.StationId)
                    .Where(r => r.ProgramId == program.ProgramId)
                    .SingleOrDefaultAsync(cancellationToken);

                if (existingProgram == null)
                {
                    await dbContext.NhkRadiruPrograms.AddAsync(program, cancellationToken);
                }
                else
                {
                    dbContext.Entry(existingProgram).CurrentValues.SetValues(program);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// らじる★らじる番組を検索する
    /// </summary>
    public async ValueTask<List<NhkRadiruProgram>> SearchRadiruProgramsAsync(
        ProgramSearchEntity searchEntity,
        DateTimeOffset standardDateTimeOffset,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.NhkRadiruPrograms.AsQueryable();

        if (searchEntity.SelectedRadiruStationIds.Count != 0)
        {
            query = query.Where(p => searchEntity.SelectedRadiruStationIds.Contains(p.AreaId + ":" + p.StationId));
        }

        if (!string.IsNullOrWhiteSpace(searchEntity.Keyword))
        {
            var keywords = searchEntity.Keyword.ParseKeywords();

            if (searchEntity.SearchTitleOnly)
            {
                query = query.Where(p => keywords.All(keyword => p.Title.Contains(keyword) || p.Subtitle.Contains(keyword)));
            }
            else
            {
                query = query.Where(
                    p =>
                        keywords.All(keyword =>
                            p.Title.Contains(keyword) ||
                            p.Subtitle.Contains(keyword) ||
                            p.Performer.Contains(keyword) ||
                            p.Description.Contains(keyword))
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(searchEntity.ExcludedKeyword))
        {
            var excludedKeywords = searchEntity.ExcludedKeyword.ParseKeywords();

            if (searchEntity.SearchTitleOnlyExcludedKeyword)
            {
                query = query.Where(p => !excludedKeywords.Any(excluded => p.Title.Contains(excluded) || p.Subtitle.Contains(excluded)));
            }
            else
            {
                query = query.Where(p =>
                    !excludedKeywords.Any(excluded =>
                        p.Title.Contains(excluded) ||
                        p.Subtitle.Contains(excluded) ||
                        p.Performer.Contains(excluded) ||
                        p.Description.Contains(excluded))
                );
            }
        }

        if (searchEntity.SelectedDaysOfWeek.Count != 0)
        {
            var selectedDays = searchEntity.SelectedDaysOfWeek.Aggregate(DaysOfWeek.None, (acc, day) => acc | day);
            query = query.Where(p => (p.DaysOfWeek & selectedDays) != DaysOfWeek.None);
        }

        var limitRadioDate = standardDateTimeOffset.AddDays(-7).ToRadioDate();

        // DateTimeOffset のSQL比較はSQLiteで期待どおりにならない場合があるため、
        // 終了済み判定はアプリ側で評価する。
        var list = (await query
                .Where(r => r.RadioDate >= limitRadioDate)
                .ToListAsync(cancellationToken))
            .Where(
                r =>
                    (searchEntity.IncludeHistoricalPrograms || r.EndTime >= standardDateTimeOffset) &&
                    r.StartTime.TimeOfDay >= searchEntity.StartTime.ToTimeSpan() &&
                    r.EndTime.TimeOfDay <= searchEntity.EndTime.ToTimeSpan())
            .OrderBy(r => r.StartTime)
            .ToList();

        return list;
    }

    /// <summary>
    /// 古いらじる★らじる番組を削除する
    /// </summary>
    public async ValueTask DeleteOldRadiruProgramsAsync(DateOnly deleteDate, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deletePrograms = await dbContext.NhkRadiruPrograms
                .Where(r => r.RadioDate < deleteDate)
                .ToListAsync(cancellationToken);

            dbContext.NhkRadiruPrograms.RemoveRange(deletePrograms);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 番組表の最終更新日時を取得する
    /// </summary>
    public async ValueTask<DateTimeOffset?> GetLastUpdatedProgramAsync(CancellationToken cancellationToken = default)
    {
        var config = await dbContext.AppConfigurations
            .Where(r => r.ConfigurationName == AppConfigurationNames.LastUpdatedProgram)
            .FirstOrDefaultAsync(cancellationToken);

        return config?.Val4;
    }

    /// <summary>
    /// 番組表の最終更新日時を更新する
    /// </summary>
    public async ValueTask SetLastUpdatedProgramAsync(DateTimeOffset dateTime, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var config = await dbContext.AppConfigurations
                .Where(r => r.ConfigurationName == AppConfigurationNames.LastUpdatedProgram)
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                config = new AppConfiguration
                {
                    ConfigurationName = AppConfigurationNames.LastUpdatedProgram,
                    Val4 = dateTime.UtcDateTime
                };

                await dbContext.AppConfigurations.AddAsync(config, cancellationToken);
            }
            else
            {
                config.Val4 = dateTime.UtcDateTime;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// スケジュール済みジョブ一覧を取得する
    /// </summary>
    public async ValueTask<List<ScheduleJob>> GetScheduleJobsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ScheduleJob
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 指定したスケジュールジョブを無効化する
    /// </summary>
    public async ValueTask<bool> DisableScheduleJobAsync(Ulid jobId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var job = await dbContext.ScheduleJob.FindAsync([jobId], cancellationToken);
            if (job == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (!job.IsEnabled)
            {
                await transaction.CommitAsync(cancellationToken);
                return true;
            }

            job.IsEnabled = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
