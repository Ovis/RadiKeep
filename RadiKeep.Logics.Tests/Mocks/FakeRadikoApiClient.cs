using RadiKeep.Logics.Interfaces;
using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Tests.Mocks;

/// <summary>
/// radiko APIクライアントのテスト用スタブ
/// </summary>
public class FakeRadikoApiClient : IRadikoApiClient
{
    public List<RadikoStation> Stations { get; set; } = [];
    public List<string> StationsByArea { get; set; } = [];
    public List<RadikoProgram> WeeklyPrograms { get; set; } = [];
    public List<string> TimeFreeUrls { get; set; } = [];
    public List<string> TimeFreeUrlsForAreaFree { get; set; } = [];

    public Task<List<RadikoStation>> GetRadikoStationsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Stations);
    }

    public Task<List<string>> GetStationsByAreaAsync(string area, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StationsByArea);
    }

    public Task<List<RadikoProgram>> GetWeeklyProgramsAsync(string stationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WeeklyPrograms);
    }

    public Task<List<string>> GetTimeFreePlaylistCreateUrlsAsync(string stationId, bool isAreaFree, CancellationToken cancellationToken = default)
    {
        var list = isAreaFree && TimeFreeUrlsForAreaFree.Count != 0
            ? TimeFreeUrlsForAreaFree
            : TimeFreeUrls;
        return Task.FromResult(list);
    }
}
