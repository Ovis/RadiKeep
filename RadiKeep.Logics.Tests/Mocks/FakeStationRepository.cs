using RadiKeep.Logics.Domain.Station;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Tests.Mocks;

/// <summary>
/// 放送局リポジトリのテスト用スタブ
/// </summary>
public class FakeStationRepository : IStationRepository
{
    public List<RadikoStation> RadikoStations { get; set; } = [];
    public NhkRadiruArea? RadiruArea { get; set; }
    public List<RadiruStationEntry> RadiruStationsFromAreaServices { get; set; } = [];
    public List<(string AreaId, string ServiceId)> ActiveRadiruAreaServiceKeys { get; set; } = [];
    public Dictionary<string, string> RadiruStreamUrls { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<bool> HasAnyRadikoStationAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(RadikoStations.Count != 0);

    public ValueTask<List<RadikoStation>> GetRadikoStationsAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(RadikoStations);

    public ValueTask AddRadikoStationsIfMissingAsync(IEnumerable<RadikoStation> stations, CancellationToken cancellationToken = default)
    {
        RadikoStations.AddRange(stations);
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> HasAnyRadiruStationAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(false);

    public ValueTask<string?> GetRadiruHlsUrlByAreaAndServiceAsync(
        string areaId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var key = $"{areaId}:{serviceId}";
        if (RadiruStreamUrls.TryGetValue(key, out var url))
        {
            return ValueTask.FromResult<string?>(url);
        }
        return ValueTask.FromResult<string?>(null);
    }

    public ValueTask<List<RadiruStationEntry>> GetRadiruStationsFromAreaServicesAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(RadiruStationsFromAreaServices);

    public ValueTask UpsertRadiruAreasAndServicesAsync(
        IEnumerable<NhkRadiruArea> areas,
        IEnumerable<NhkRadiruAreaService> services,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask<List<(string AreaId, string ServiceId)>> GetActiveRadiruAreaServiceKeysAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ActiveRadiruAreaServiceKeys);

    public ValueTask<NhkRadiruArea?> GetRadiruAreaByAreaIdAsync(string areaId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(RadiruArea);
}
