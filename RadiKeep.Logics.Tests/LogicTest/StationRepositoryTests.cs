using Microsoft.EntityFrameworkCore;
using RadiKeep.Logics.Infrastructure.Station;
using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Tests.LogicTest;

/// <summary>
/// StationRepositoryのテスト
/// </summary>
public class StationRepositoryTests : UnitTestBase
{
    private RadioDbContext _dbContext = null!;
    private StationRepository _repository = null!;

    [SetUp]
    public async Task Setup()
    {
        _dbContext = DbContext;
        _dbContext.ChangeTracker.Clear();
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruAreaServices");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruAreas");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruStations");
        _repository = new StationRepository(_dbContext);
    }

    /// <summary>
    /// radiko放送局の存在確認と取得
    /// </summary>
    [Test]
    public async Task RadikoStations_取得確認()
    {
        var hasAnyBefore = await _repository.HasAnyRadikoStationAsync();
        Assert.That(hasAnyBefore, Is.False);

        await _repository.AddRadikoStationsIfMissingAsync([
            new RadikoStation { StationId = "TBS", RegionId = "JP13" }
        ]);

        var hasAnyAfter = await _repository.HasAnyRadikoStationAsync();
        Assert.That(hasAnyAfter, Is.True);

        var list = await _repository.GetRadikoStationsAsync();
        Assert.That(list.Count, Is.EqualTo(1));
    }

    /// <summary>
    /// らじる★らじる放送局の追加/取得
    /// </summary>
    [Test]
    public async Task RadiruStations_追加更新取得()
    {
        var hasAnyBefore = await _repository.HasAnyRadiruStationAsync();
        Assert.That(hasAnyBefore, Is.False);

        await _repository.UpsertRadiruStationsAsync([
            new NhkRadiruStation { AreaId = "JP13", ApiKey = "JP13", AreaJpName = "東京", R1Hls = "r1" }
        ]);

        var hasAnyAfter = await _repository.HasAnyRadiruStationAsync();
        Assert.That(hasAnyAfter, Is.True);

        var station = await _repository.GetRadiruStationByAreaAsync("JP13");
        Assert.That(station.AreaId, Is.EqualTo("JP13"));
    }

    [Test]
    public async Task GetRadiruHlsUrlByAreaAndServiceAsync_新テーブルを優先する()
    {
        _dbContext.NhkRadiruStations.Add(new NhkRadiruStation
        {
            AreaId = "JP13",
            ApiKey = "JP13",
            AreaJpName = "東京",
            R1Hls = "https://legacy.example/r1.m3u8"
        });
        _dbContext.NhkRadiruAreas.Add(new NhkRadiruArea
        {
            AreaId = "JP13",
            ApiKey = "JP13",
            AreaJpName = "東京",
            ProgramNowOnAirApiUrl = "https://example/noa",
            ProgramDetailApiUrlTemplate = "https://example/detail/{area}",
            DailyProgramApiUrlTemplate = "https://example/day/{area}"
        });
        _dbContext.NhkRadiruAreaServices.Add(new NhkRadiruAreaService
        {
            AreaId = "JP13",
            ServiceId = "r1",
            ServiceName = "R1",
            HlsUrl = "https://new.example/r1.m3u8",
            IsActive = true
        });
        await _dbContext.SaveChangesAsync();

        var url = await _repository.GetRadiruHlsUrlByAreaAndServiceAsync("JP13", "r1");

        Assert.That(url, Is.EqualTo("https://new.example/r1.m3u8"));
    }

    [Test]
    public async Task GetRadiruHlsUrlByAreaAndServiceAsync_新テーブル未登録時は旧テーブルにフォールバック()
    {
        _dbContext.NhkRadiruStations.Add(new NhkRadiruStation
        {
            AreaId = "JP13",
            ApiKey = "JP13",
            AreaJpName = "東京",
            R1Hls = "https://legacy.example/r1.m3u8"
        });
        await _dbContext.SaveChangesAsync();

        var url = await _repository.GetRadiruHlsUrlByAreaAndServiceAsync("JP13", "r1");

        Assert.That(url, Is.EqualTo("https://legacy.example/r1.m3u8"));
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.ChangeTracker.Clear();
    }
}
