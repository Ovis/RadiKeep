using Microsoft.EntityFrameworkCore;
using RadiKeep.Logics.Infrastructure.ProgramSchedule;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Models.Radiko;
using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Tests.LogicTest;

/// <summary>
/// ProgramScheduleRepositoryのテスト
/// </summary>
public class ProgramScheduleRepositoryTests : UnitTestBase
{
    private RadioDbContext _dbContext = null!;
    private ProgramScheduleRepository _repository = null!;

    [SetUp]
    public async Task Setup()
    {
        _dbContext = DbContext;
        _dbContext.ChangeTracker.Clear();
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoPrograms");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RadikoStations");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruPrograms");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM NhkRadiruStations");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ScheduleJob");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM AppConfigurations");
        _repository = new ProgramScheduleRepository(_dbContext);
    }

    /// <summary>
    /// 放送中のradiko番組を取得できる
    /// </summary>
    [Test]
    public async Task GetRadikoNowOnAirAsync_放送中だけ取得()
    {
        var now = DateTimeOffset.UtcNow;
        await AddRadikoProgramAsync("P1", "TBS", now.AddMinutes(-5), now.AddMinutes(5));
        await AddRadikoProgramAsync("P2", "TBS", now.AddHours(-2), now.AddHours(-1));

        var list = await _repository.GetRadikoNowOnAirAsync(now);

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].ProgramId, Is.EqualTo("P1"));
    }

    /// <summary>
    /// radiko番組一覧取得ができる
    /// </summary>
    [Test]
    public async Task GetRadikoProgramsAsync_日付局で取得()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        await AddRadikoProgramAsync("P1", "TBS", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), date: date);
        await AddRadikoProgramAsync("P2", "ABC", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), date: date);

        var list = await _repository.GetRadikoProgramsAsync(date, "TBS");

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].StationId, Is.EqualTo("TBS"));
    }

    /// <summary>
    /// radiko番組IDで取得できる
    /// </summary>
    [Test]
    public async Task GetRadikoProgramByIdAsync_取得できる()
    {
        await AddRadikoProgramAsync("P100", "TBS", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));

        var program = await _repository.GetRadikoProgramByIdAsync("P100");

        Assert.That(program, Is.Not.Null);
        Assert.That(program!.ProgramId, Is.EqualTo("P100"));
    }

    /// <summary>
    /// radiko番組の重複追加を避ける
    /// </summary>
    [Test]
    public async Task AddRadikoProgramsIfMissingAsync_重複回避()
    {
        var existing = await AddRadikoProgramAsync("P1", "TBS", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));
        var newProgram = CreateRadikoProgram("P2", "TBS", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));

        await _repository.AddRadikoProgramsIfMissingAsync([existing, newProgram]);

        var count = await _dbContext.RadikoPrograms.CountAsync();
        Assert.That(count, Is.EqualTo(2));
    }

    /// <summary>
    /// radiko番組検索ができる
    /// </summary>
    [Test]
    public async Task SearchRadikoProgramsAsync_キーワード検索()
    {
        var now = DateTimeOffset.UtcNow;
        await AddRadikoProgramAsync("P1", "TBS", now.AddMinutes(-10), now.AddMinutes(10), title: "Alpha", description: "desc");
        await AddRadikoProgramAsync("P2", "TBS", now.AddMinutes(-10), now.AddMinutes(10), title: "Beta", description: "NG");

        var entity = new ProgramSearchEntity
        {
            Keyword = "Alpha",
            SearchTitleOnly = true,
            ExcludedKeyword = "NG",
            SearchTitleOnlyExcludedKeyword = false,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = false
        };

        var list = await _repository.SearchRadikoProgramsAsync(entity, now);

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].ProgramId, Is.EqualTo("P1"));
    }

    /// <summary>
    /// radiko番組検索で除外キーワードはいずれか一致で除外される
    /// </summary>
    [Test]
    public async Task SearchRadikoProgramsAsync_除外キーワード複数は一語一致でも除外()
    {
        var now = DateTimeOffset.UtcNow;
        await AddRadikoProgramAsync("P1", "TBS", now.AddMinutes(-10), now.AddMinutes(10), title: "Alpha", description: "contains NG1");
        await AddRadikoProgramAsync("P2", "TBS", now.AddMinutes(-10), now.AddMinutes(10), title: "Beta", description: "safe");

        var entity = new ProgramSearchEntity
        {
            ExcludedKeyword = "NG1 NG2",
            SearchTitleOnlyExcludedKeyword = false,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = false
        };

        var list = await _repository.SearchRadikoProgramsAsync(entity, now);

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].ProgramId, Is.EqualTo("P2"));
    }

    /// <summary>
    /// radiko番組検索で録音可能な番組のみ抽出できる
    /// </summary>
    [Test]
    public async Task SearchRadikoProgramsAsync_録音可能のみ抽出()
    {
        var now = new DateTimeOffset(2026, 2, 11, 12, 0, 0, TimeSpan.FromHours(9));
        await AddRadikoProgramAsync(
            "P1",
            "TBS",
            now.AddHours(-2),
            now.AddHours(-1),
            title: "Ended",
            description: "desc",
            availabilityTimeFree: AvailabilityTimeFree.Unavailable);
        await AddRadikoProgramAsync(
            "P2",
            "TBS",
            now.AddHours(-4),
            now.AddHours(-3),
            title: "TimeFree",
            description: "desc",
            availabilityTimeFree: AvailabilityTimeFree.Available);
        await AddRadikoProgramAsync(
            "P3",
            "TBS",
            now.AddMinutes(-10),
            now.AddMinutes(20),
            title: "Live",
            description: "desc",
            availabilityTimeFree: AvailabilityTimeFree.Unavailable);

        var entity = new ProgramSearchEntity
        {
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = true,
            RecordableOnly = true
        };

        var list = await _repository.SearchRadikoProgramsAsync(entity, now);

        Assert.That(list.Select(x => x.ProgramId), Is.EqualTo(new[] { "P2", "P3" }));
    }

    /// <summary>
    /// 古いradiko番組を削除できる
    /// </summary>
    [Test]
    public async Task DeleteOldRadikoProgramsAsync_削除()
    {
        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2));
        await AddRadikoProgramAsync("P1", "TBS", DateTimeOffset.UtcNow.AddMonths(-2), DateTimeOffset.UtcNow.AddMonths(-2).AddMinutes(10), date: oldDate);

        await _repository.DeleteOldRadikoProgramsAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)));

        var count = await _dbContext.RadikoPrograms.CountAsync();
        Assert.That(count, Is.EqualTo(0));
    }

    /// <summary>
    /// らじる★らじる番組を追加/更新できる
    /// </summary>
    [Test]
    public async Task UpsertRadiruProgramsAsync_追加更新()
    {
        var program = CreateRadiruProgram("R1_1", "JP13", "r1", "TitleA");
        await _repository.UpsertRadiruProgramsAsync([program]);

        _dbContext.ChangeTracker.Clear();

        program.Title = "TitleB";
        await _repository.UpsertRadiruProgramsAsync([program]);

        var result = await _repository.GetRadiruProgramByIdAsync("R1_1");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Title, Is.EqualTo("TitleB"));
    }

    /// <summary>
    /// らじる★らじる番組検索ができる
    /// </summary>
    [Test]
    public async Task SearchRadiruProgramsAsync_キーワード検索()
    {
        var now = DateTimeOffset.UtcNow;
        await AddRadiruProgramAsync("R1_1", "JP13", "r1", now.AddMinutes(-10), now.AddMinutes(10), title: "Alpha");
        await AddRadiruProgramAsync("R1_2", "JP13", "r1", now.AddMinutes(-10), now.AddMinutes(10), title: "Beta", description: "NG");

        var entity = new ProgramSearchEntity
        {
            SelectedRadiruStationIds = ["JP13:r1"],
            Keyword = "Alpha",
            SearchTitleOnly = true,
            ExcludedKeyword = "NG",
            SearchTitleOnlyExcludedKeyword = false,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = false
        };

        var list = await _repository.SearchRadiruProgramsAsync(entity, now);

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].ProgramId, Is.EqualTo("R1_1"));
    }

    /// <summary>
    /// らじる★らじる検索で除外キーワードはいずれか一致で除外される
    /// </summary>
    [Test]
    public async Task SearchRadiruProgramsAsync_除外キーワード複数は一語一致でも除外()
    {
        var now = DateTimeOffset.UtcNow;
        await AddRadiruProgramAsync("R1_1", "JP13", "r1", now.AddMinutes(-10), now.AddMinutes(10), title: "Alpha", description: "contains NG1");
        await AddRadiruProgramAsync("R1_2", "JP13", "r1", now.AddMinutes(-10), now.AddMinutes(10), title: "Beta", description: "safe");

        var entity = new ProgramSearchEntity
        {
            SelectedRadiruStationIds = ["JP13:r1"],
            ExcludedKeyword = "NG1 NG2",
            SearchTitleOnlyExcludedKeyword = false,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = false
        };

        var list = await _repository.SearchRadiruProgramsAsync(entity, now);

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].ProgramId, Is.EqualTo("R1_2"));
    }

    /// <summary>
    /// らじる★らじる検索で放送済み番組を除外できる
    /// </summary>
    [Test]
    public async Task SearchRadiruProgramsAsync_放送済み除外()
    {
        var now = new DateTimeOffset(2026, 2, 11, 12, 0, 0, TimeSpan.FromHours(9));
        await AddRadiruProgramAsync("R1_1", "JP13", "r1", now.AddHours(-2), now.AddHours(-1), title: "Ended");
        await AddRadiruProgramAsync("R1_2", "JP13", "r1", now.AddMinutes(-10), now.AddMinutes(20), title: "Now");

        var entity = new ProgramSearchEntity
        {
            SelectedRadiruStationIds = ["JP13:r1"],
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = false
        };

        var list = await _repository.SearchRadiruProgramsAsync(entity, now);

        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list[0].ProgramId, Is.EqualTo("R1_2"));
    }

    /// <summary>
    /// らじる★らじる検索で録音可能な番組のみ抽出できる
    /// </summary>
    [Test]
    public async Task SearchRadiruProgramsAsync_録音可能のみ抽出()
    {
        var now = new DateTimeOffset(2026, 2, 11, 12, 0, 0, TimeSpan.FromHours(9));
        await AddRadiruProgramAsync("R1_1", "JP13", "r1", now.AddHours(-2), now.AddHours(-1), title: "Ended");
        await AddRadiruProgramAsync(
            "R1_2",
            "JP13",
            "r1",
            now.AddHours(-3),
            now.AddHours(-2),
            title: "OnDemand",
            onDemandContentUrl: "https://example.com/stream.m3u8",
            onDemandExpiresAtUtc: now.UtcDateTime.AddHours(1));
        await AddRadiruProgramAsync("R1_3", "JP13", "r1", now.AddMinutes(-10), now.AddMinutes(20), title: "Live");

        var entity = new ProgramSearchEntity
        {
            SelectedRadiruStationIds = ["JP13:r1"],
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IncludeHistoricalPrograms = true,
            RecordableOnly = true
        };

        var list = await _repository.SearchRadiruProgramsAsync(entity, now);

        Assert.That(list.Select(x => x.ProgramId), Is.EqualTo(new[] { "R1_2", "R1_3" }));
    }

    /// <summary>
    /// 最終更新日時を保存/取得できる
    /// </summary>
    [Test]
    public async Task SetAndGetLastUpdatedProgramAsync_保存取得()
    {
        var dt = DateTimeOffset.UtcNow;

        await _repository.SetLastUpdatedProgramAsync(dt);

        var stored = await _repository.GetLastUpdatedProgramAsync();
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Value.UtcDateTime, Is.EqualTo(dt.UtcDateTime));
    }

    /// <summary>
    /// スケジュールジョブ一覧を取得できる
    /// </summary>
    [Test]
    public async Task GetScheduleJobsAsync_取得()
    {
        _dbContext.ScheduleJob.Add(new ScheduleJob
        {
            Id = Ulid.NewUlid(),
            ServiceKind = RadioServiceKind.Radiko,
            StationId = "TBS",
            ProgramId = "P1",
            Title = "Test",
            StartDateTime = DateTime.UtcNow,
            EndDateTime = DateTime.UtcNow.AddMinutes(30),
            RecordingType = RecordingType.RealTime,
            ReserveType = ReserveType.Program,
            IsEnabled = true
        });
        await _dbContext.SaveChangesAsync();

        var list = await _repository.GetScheduleJobsAsync();

        Assert.That(list.Count, Is.EqualTo(1));
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// radiko番組を追加
    /// </summary>
    private async Task<RadikoProgram> AddRadikoProgramAsync(
        string programId,
        string stationId,
        DateTimeOffset start,
        DateTimeOffset end,
        string title = "Title",
        string description = "desc",
        DateOnly? date = null,
        AvailabilityTimeFree availabilityTimeFree = AvailabilityTimeFree.Available)
    {
        var program = CreateRadikoProgram(programId, stationId, start, end, title, description, date, availabilityTimeFree);
        _dbContext.RadikoPrograms.Add(program);
        await _dbContext.SaveChangesAsync();
        return program;
    }

    /// <summary>
    /// radiko番組の作成
    /// </summary>
    private static RadikoProgram CreateRadikoProgram(
        string programId,
        string stationId,
        DateTimeOffset start,
        DateTimeOffset end,
        string title = "Title",
        string description = "desc",
        DateOnly? date = null,
        AvailabilityTimeFree availabilityTimeFree = AvailabilityTimeFree.Available)
    {
        return new RadikoProgram
        {
            ProgramId = programId,
            StationId = stationId,
            Title = title,
            Description = description,
            Performer = "",
            RadioDate = date ?? DateOnly.FromDateTime(start.UtcDateTime),
            DaysOfWeek = DaysOfWeek.Monday,
            StartTime = start.UtcDateTime,
            EndTime = end.UtcDateTime,
            AvailabilityTimeFree = availabilityTimeFree,
            ProgramUrl = ""
        };
    }

    /// <summary>
    /// らじる番組を追加
    /// </summary>
    private async Task AddRadiruProgramAsync(
        string programId,
        string areaId,
        string stationId,
        DateTimeOffset start,
        DateTimeOffset end,
        string title = "Title",
        string description = "desc",
        string? onDemandContentUrl = null,
        DateTime? onDemandExpiresAtUtc = null)
    {
        _dbContext.NhkRadiruPrograms.Add(new NhkRadiruProgram
        {
            ProgramId = programId,
            AreaId = areaId,
            StationId = stationId,
            Title = title,
            Subtitle = "",
            RadioDate = DateOnly.FromDateTime(start.UtcDateTime),
            DaysOfWeek = DaysOfWeek.Monday,
            StartTime = start.UtcDateTime,
            EndTime = end.UtcDateTime,
            Performer = "",
            Description = description,
            EventId = "",
            SiteId = "",
            ProgramUrl = "",
            ImageUrl = "",
            OnDemandContentUrl = onDemandContentUrl,
            OnDemandExpiresAtUtc = onDemandExpiresAtUtc
        });
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// らじる番組の作成
    /// </summary>
    private static NhkRadiruProgram CreateRadiruProgram(
        string programId,
        string areaId,
        string stationId,
        string title)
    {
        return new NhkRadiruProgram
        {
            ProgramId = programId,
            AreaId = areaId,
            StationId = stationId,
            Title = title,
            Subtitle = "",
            RadioDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DaysOfWeek = DaysOfWeek.Monday,
            StartTime = DateTimeOffset.UtcNow.UtcDateTime,
            EndTime = DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime,
            Performer = "",
            Description = "",
            EventId = "",
            SiteId = "",
            ProgramUrl = "",
            ImageUrl = ""
        };
    }
}
