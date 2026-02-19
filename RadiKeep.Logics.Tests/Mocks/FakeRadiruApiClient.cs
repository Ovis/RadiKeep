using RadiKeep.Logics.Interfaces;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Models.NhkRadiru.JsonEntity;

namespace RadiKeep.Logics.Tests.Mocks;

/// <summary>
/// らじるAPIクライアントのテスト用スタブ
/// </summary>
public class FakeRadiruApiClient : IRadiruApiClient
{
    public List<RadiruProgramJsonEntity> Programs { get; set; } = [];

    public Task<List<RadiruProgramJsonEntity>> GetDailyProgramsAsync(
        RadiruAreaKind area,
        RadiruStationKind stationKind,
        DateTimeOffset date,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Programs);
    }
}
