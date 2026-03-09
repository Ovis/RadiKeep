using Microsoft.EntityFrameworkCore;
using RadiKeep.Logics.Domain.Station;
using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Infrastructure.Station;

/// <summary>
/// 放送局情報の永続化を担うリポジトリ実装
/// </summary>
public class StationRepository(RadioDbContext dbContext) : IStationRepository
{
    /// <summary>
    /// radiko放送局が初期化済みか確認する
    /// </summary>
    public async ValueTask<bool> HasAnyRadikoStationAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.RadikoStations
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// radiko放送局を取得する
    /// </summary>
    public async ValueTask<List<RadikoStation>> GetRadikoStationsAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var list = await dbContext.RadikoStations
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return list;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// radiko放送局を追加する
    /// </summary>
    public async ValueTask AddRadikoStationsIfMissingAsync(IEnumerable<RadikoStation> stations, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var station in stations)
            {
                var existing = await dbContext.RadikoStations.FindAsync([station.StationId], cancellationToken);

                if (existing == null)
                {
                    await dbContext.RadikoStations.AddAsync(station, cancellationToken);
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
    /// らじる★らじる放送局が初期化済みか確認する
    /// </summary>
    public async ValueTask<bool> HasAnyRadiruStationAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var hasStationData = await dbContext.NhkRadiruStations
                .AnyAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return hasStationData;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// らじる★らじる放送局を追加または更新する
    /// </summary>
    public async ValueTask UpsertRadiruStationsAsync(IEnumerable<NhkRadiruStation> stations, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var station in stations)
            {
                var stationEntry =
                    await dbContext.NhkRadiruStations
                        .Where(r => r.AreaId == station.AreaId)
                        .SingleOrDefaultAsync(cancellationToken);

                if (stationEntry == null)
                {
                    await dbContext.NhkRadiruStations.AddAsync(station, cancellationToken);
                }
                else
                {
                    dbContext.Entry(stationEntry).CurrentValues.SetValues(station);
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
    /// 指定エリアのらじる★らじる放送局情報を取得する
    /// </summary>
    public async ValueTask<NhkRadiruStation> GetRadiruStationByAreaAsync(string areaId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var station = await dbContext.NhkRadiruStations
                .Where(r => r.AreaId == areaId)
                .SingleAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return station;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 指定エリアとサービスIDに対応するらじる★らじるHLS URLを取得する
    /// </summary>
    public async ValueTask<string?> GetRadiruHlsUrlByAreaAndServiceAsync(
        string areaId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedServiceId = serviceId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(areaId) || string.IsNullOrWhiteSpace(normalizedServiceId))
        {
            return null;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var serviceUrl = await dbContext.NhkRadiruAreaServices
                .AsNoTracking()
                .Where(s => s.AreaId == areaId && s.IsActive)
                .Where(s => s.ServiceId.ToLower() == normalizedServiceId)
                .Select(s => s.HlsUrl)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(serviceUrl))
            {
                await transaction.CommitAsync(cancellationToken);
                return serviceUrl;
            }

            var legacyStation = await dbContext.NhkRadiruStations
                .AsNoTracking()
                .Where(r => r.AreaId == areaId)
                .SingleOrDefaultAsync(cancellationToken);

            var legacyUrl = legacyStation is null
                ? null
                : ResolveLegacyRadiruHlsUrl(legacyStation, normalizedServiceId);

            await transaction.CommitAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(legacyUrl) ? null : legacyUrl;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? ResolveLegacyRadiruHlsUrl(NhkRadiruStation station, string normalizedServiceId)
    {
        return normalizedServiceId switch
        {
            "r1" => station.R1Hls,
            "r2" => station.R2Hls,
            "r3" => station.FmHls,
            _ => null
        };
    }
}
