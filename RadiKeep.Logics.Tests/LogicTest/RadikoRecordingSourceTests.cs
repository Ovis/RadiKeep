using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Domain.ProgramSchedule;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Infrastructure.Recording;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.RecordJobLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Tests.Mocks;

namespace RadiKeep.Logics.Tests.LogicTest;

public class RadikoRecordingSourceTests
{
    private RadioDbContext _dbContext = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<RadioDbContext>()
            .UseSqlite("Data Source=radiko-recording-source.db")
            .Options;
        _dbContext = new RadioDbContext(options);
        _dbContext.Database.EnsureDeleted();
        _dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private static IHttpClientFactory CreateHttpClientFactory(string area, bool isAreaFree)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddHandler(
            req => req.RequestUri!.ToString().StartsWith("http://radiko.jp/area/"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(area) });

        var loginJson = $"{{\"radiko_session\":\"session\",\"paid_member\":\"{(isAreaFree ? "1" : "0")}\",\"areafree\":\"{(isAreaFree ? "1" : "0")}\"}}";
        handler.AddHandler(
            req => req.RequestUri!.ToString().StartsWith("https://radiko.jp/ap/member/webapi/member/login"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(loginJson) });

        handler.AddHandler(
            req => req.RequestUri!.ToString().StartsWith("https://radiko.jp/v2/api/auth1"),
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Add("X-Radiko-AuthToken", "token");
                response.Headers.Add("X-Radiko-KeyLength", "4");
                response.Headers.Add("X-Radiko-KeyOffset", "0");
                return response;
            });

        handler.AddHandler(
            req => req.RequestUri!.ToString().StartsWith("http://radiko.jp/apps/js/playerCommon.js"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("new RadikoJSPlayer('a','b','0123456789ABCDEF'){") });

        handler.AddHandler(
            req => req.RequestUri!.ToString().StartsWith("https://radiko.jp/v2/api/auth2"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{area},0,0\n") });

        return new FakeHttpClientFactory(new HttpClient(handler));
    }

    private static IAppConfigurationService CreateConfig(string stationId)
    {
        var configMock = new Mock<IAppConfigurationService>();
        configMock.SetupGet(c => c.RadikoOptions).Returns(new Options.RadikoOptions
        {
            RadikoUserId = "user",
            RadikoPassword = "pass"
        });
        configMock.Setup(c => c.TryGetRadikoCredentialsAsync())
            .Returns(ValueTask.FromResult((true, "user", "pass")));
        var dic = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        dic.TryAdd(stationId, "Test Station");
        configMock.SetupGet(c => c.RadikoStationDic).Returns(dic);
        configMock.SetupGet(c => c.IsRadikoAreaFree).Returns(false);
        configMock.Setup(c => c.UpdateRadikoPremiumUser(It.IsAny<bool>()));
        configMock.Setup(c => c.UpdateRadikoAreaFree(It.IsAny<bool>()));
        return configMock.Object;
    }

    private static ProgramScheduleLobLogic CreateProgramScheduleLobLogic(
        IRadioAppContext appContext,
        IProgramScheduleRepository repository,
        IEntryMapper entryMapper)
    {
        var logger = new Mock<ILogger<ProgramScheduleLobLogic>>().Object;
        var radikoApiClient = new FakeRadikoApiClient();
        var radiruApiClient = new FakeRadiruApiClient();

        var configMock = new Mock<IAppConfigurationService>();
        configMock.SetupGet(c => c.RecordStartDuration).Returns(TimeSpan.Zero);
        configMock.SetupGet(c => c.RecordEndDuration).Returns(TimeSpan.Zero);

        var recordJobLobLogic = new RecordJobLobLogic(
            new Mock<ILogger<RecordJobLobLogic>>().Object,
            configMock.Object);

        return new ProgramScheduleLobLogic(
            logger,
            appContext,
            radikoApiClient,
            radiruApiClient,
            repository,
            recordJobLobLogic,
            entryMapper);
    }

    private RadikoRecordingSource CreateTarget(
        string stationId,
        string area,
        bool isAreaFree,
        List<string> currentAreaStations,
        List<string> timeFreeUrls)
    {
        var config = CreateConfig(stationId);
        var entryMapper = new EntryMapper(config);

        var httpClientFactory = CreateHttpClientFactory(area, isAreaFree);
        var radikoLogic = new RadikoUniqueProcessLogic(
            new Mock<ILogger<RadikoUniqueProcessLogic>>().Object,
            config,
            httpClientFactory);

        var stationRepository = new FakeStationRepository();
        var radikoApiClient = new FakeRadikoApiClient
        {
            StationsByArea = currentAreaStations,
            TimeFreeUrls = timeFreeUrls,
            TimeFreeUrlsForAreaFree = timeFreeUrls
        };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new HttpClientHandler()));

        var stationLogic = new StationLobLogic(
            new Mock<ILogger<StationLobLogic>>().Object,
            config,
            radikoApiClient,
            stationRepository,
            radikoLogic,
            httpClientFactoryMock.Object,
            entryMapper);

        var repository = new FakeProgramScheduleRepository
        {
            RadikoProgramById = new RadikoProgram
            {
                ProgramId = "P1",
                StationId = stationId,
                Title = "Test",
                Performer = "P",
                Description = "D",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddMinutes(30),
                ProgramUrl = "http://example"
            }
        };

        var appContext = new FakeRadioAppContext();
        var programScheduleLogic = CreateProgramScheduleLobLogic(appContext, repository, entryMapper);

        return new RadikoRecordingSource(
            new Mock<ILogger<RadikoRecordingSource>>().Object,
            programScheduleLogic,
            stationLogic,
            radikoLogic,
            radikoApiClient,
            _dbContext);
    }

    /// <summary>
    /// 異常系: エリア外かつエリアフリー不可の場合は録音不可
    /// </summary>
    [Test]
    public async Task PrepareAsync_OutOfAreaWithoutAreaFree_Throws()
    {
        var stationId = "OUT";
        _dbContext.RadikoStations.Add(new RadikoStation
        {
            StationId = stationId,
            StationName = "Out Station",
            Area = "JP13",
            RegionId = "R1",
            RegionName = "Region",
            RegionOrder = 1,
            StationOrder = 1,
            AreaFree = false,
            TimeFree = true
        });
        await _dbContext.SaveChangesAsync();

        var target = CreateTarget(
            stationId,
            area: "JP13",
            isAreaFree: false,
            currentAreaStations: ["IN"],
            timeFreeUrls: ["http://example/timefree.m3u8"]);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P1",
            ProgramName: "Test",
            IsTimeFree: true,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var ex = Assert.ThrowsAsync<DomainException>(async () => await target.PrepareAsync(command));
        Assert.That(ex!.Message, Is.EqualTo("この番組は地域が異なるため録音できません。プレミアム会員でのログインが必要です。"));
    }

    /// <summary>
    /// 正常系: タイムフリー録音のURLとヘッダを取得できる
    /// </summary>
    [Test]
    public async Task PrepareAsync_TimeFree_ReturnsUrlAndHeaders()
    {
        var stationId = "IN";
        _dbContext.RadikoStations.Add(new RadikoStation
        {
            StationId = stationId,
            StationName = "In Station",
            Area = "JP13",
            RegionId = "R1",
            RegionName = "Region",
            RegionOrder = 1,
            StationOrder = 1,
            AreaFree = true,
            TimeFree = true
        });
        await _dbContext.SaveChangesAsync();

        var target = CreateTarget(
            stationId,
            area: "JP13",
            isAreaFree: true,
            currentAreaStations: ["IN"],
            timeFreeUrls: ["http://example/timefree.m3u8"]);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P1",
            ProgramName: "Test",
            IsTimeFree: true,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await target.PrepareAsync(command);

        Assert.That(result.StreamUrl, Is.EqualTo("http://example/timefree.m3u8"));
        Assert.That(result.Headers.ContainsKey("X-Radiko-Authtoken"), Is.True);
        Assert.That(result.Headers.ContainsKey("X-Radiko-AreaId"), Is.True);
        Assert.That(result.Options.IsTimeFree, Is.True);
        Assert.That(result.ProgramInfo.StationId, Is.EqualTo(stationId));
    }
}

