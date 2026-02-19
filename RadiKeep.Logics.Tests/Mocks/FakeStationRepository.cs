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
    public NhkRadiruStation RadiruStation { get; set; } = new();

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

    public ValueTask UpsertRadiruStationsAsync(IEnumerable<NhkRadiruStation> stations, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask<NhkRadiruStation> GetRadiruStationByAreaAsync(string areaId, CancellationToken cancellationToken = default)
    {
        RadiruStation.AreaId = areaId;
        return ValueTask.FromResult(RadiruStation);
    }
}
