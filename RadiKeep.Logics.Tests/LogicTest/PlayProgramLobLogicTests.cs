using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Domain.ProgramSchedule;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.PlayProgramLogic;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Logics.RecordJobLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Models.Radiko;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Tests.Mocks;

namespace RadiKeep.Logics.Tests.LogicTest;

public class PlayProgramLobLogicTests
{
    private RadioDbContext _dbContext = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<RadioDbContext>()
            .UseSqlite("Data Source=play-program.db")
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

    private static IHttpClientFactory CreateHttpClientFactory(string area, bool isPremium)
    {
        var handler = new FakeHttpMessageHandler();

        handler.AddHandler(
            req => req.RequestUri!.ToString().StartsWith("http://radiko.jp/area/"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(area) });

        var loginJson = $"{{\"radiko_session\":\"session\",\"paid_member\":\"{(isPremium ? "1" : "0")}\",\"areafree\":\"{(isPremium ? "1" : "0")}\"}}";
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

        var schedulerFactory = new Mock<ISchedulerFactory>().Object;
        var recordJobLobLogic = new RecordJobLobLogic(
            new Mock<ILogger<RecordJobLobLogic>>().Object,
            schedulerFactory,
            configMock.Object,
            appContext);

        return new ProgramScheduleLobLogic(
            logger,
            appContext,
            radikoApiClient,
            radiruApiClient,
            repository,
            recordJobLobLogic,
            entryMapper);
    }

    private PlayProgramLobLogic CreateTarget(
        string stationId,
        bool isPremium,
        List<string> currentAreaStations,
        IProgramScheduleRepository repository)
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
        dic.TryAdd(stationId, "Station");
        configMock.SetupGet(c => c.RadikoStationDic).Returns(dic);
        configMock.Setup(c => c.UpdateRadikoPremiumUser(It.IsAny<bool>()));
        configMock.Setup(c => c.UpdateRadikoAreaFree(It.IsAny<bool>()));

        var entryMapper = new EntryMapper(configMock.Object);
        var appContext = new FakeRadioAppContext();

        var httpClientFactory = CreateHttpClientFactory("JP13", isPremium);
        var radikoLogic = new RadikoUniqueProcessLogic(
            new Mock<ILogger<RadikoUniqueProcessLogic>>().Object,
            configMock.Object,
            httpClientFactory);

        var radikoApiClient = new FakeRadikoApiClient { StationsByArea = currentAreaStations };
        var stationRepository = new FakeStationRepository();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new HttpClientHandler()));
        var stationLogic = new StationLobLogic(
            new Mock<ILogger<StationLobLogic>>().Object,
            configMock.Object,
            radikoApiClient,
            stationRepository,
            radikoLogic,
            httpClientFactoryMock.Object,
            entryMapper);

        var programScheduleLogic = CreateProgramScheduleLobLogic(appContext, repository, entryMapper);

        return new PlayProgramLobLogic(
            new Mock<ILogger<RecordingLobLogic>>().Object,
            _dbContext,
            radikoLogic,
            programScheduleLogic,
            stationLogic);
    }

    [Test]
    public async Task PlayRadikoProgramAsync_番組が存在しない場合は失敗()
    {
        var repository = new FakeProgramScheduleRepository
        {
            RadikoProgramById = null
        };

        var logic = CreateTarget("TBS", isPremium: true, currentAreaStations: ["TBS"], repository);

        var (isSuccess, _, _, error) = await logic.PlayRadikoProgramAsync("P1");

        Assert.That(isSuccess, Is.False);
        Assert.That(error, Is.TypeOf<DomainException>());
        Assert.That(error!.Message, Is.EqualTo("番組情報の取得に失敗しました。"));
    }

    [Test]
    public async Task PlayRadikoProgramAsync_エリア外かつ非プレミアムは失敗()
    {
        var repository = new FakeProgramScheduleRepository
        {
            RadikoProgramById = new RadikoProgram
            {
                ProgramId = "P1",
                StationId = "OUT",
                Title = "Test",
                RadioDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DaysOfWeek = DaysOfWeek.Monday,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddMinutes(30),
                AvailabilityTimeFree = AvailabilityTimeFree.Available
            }
        };

        _dbContext.RadikoStations.Add(new RadikoStation
        {
            StationId = "OUT",
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

        var logic = CreateTarget("OUT", isPremium: false, currentAreaStations: ["IN"], repository);

        var (isSuccess, _, _, error) = await logic.PlayRadikoProgramAsync("P1");

        Assert.That(isSuccess, Is.False);
        Assert.That(error, Is.TypeOf<DomainException>());
        Assert.That(error!.Message, Is.EqualTo("この番組は地域が異なるため再生できませんでした。異なる地域の番組を再生する場合はプレミアム会員としてログインする必要があります。"));
    }

    [Test]
    public async Task PlayRadikoProgramAsync_成功時はトークンとURLを返す()
    {
        var repository = new FakeProgramScheduleRepository
        {
            RadikoProgramById = new RadikoProgram
            {
                ProgramId = "P1",
                StationId = "TBS",
                Title = "Test",
                RadioDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DaysOfWeek = DaysOfWeek.Monday,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddMinutes(30),
                AvailabilityTimeFree = AvailabilityTimeFree.Available
            }
        };

        _dbContext.RadikoStations.Add(new RadikoStation
        {
            StationId = "TBS",
            StationName = "TBS",
            Area = "JP13",
            RegionId = "R1",
            RegionName = "Region",
            RegionOrder = 1,
            StationOrder = 1,
            AreaFree = true,
            TimeFree = true
        });
        await _dbContext.SaveChangesAsync();

        var logic = CreateTarget("TBS", isPremium: true, currentAreaStations: ["TBS"], repository);

        var (isSuccess, token, url, error) = await logic.PlayRadikoProgramAsync("P1");

        Assert.That(isSuccess, Is.True);
        Assert.That(error, Is.Null);
        Assert.That(token, Is.EqualTo("token"));
        Assert.That(url, Is.EqualTo("https://f-radiko.smartstream.ne.jp/TBS/_definst_/simul-stream.stream/playlist.m3u8"));
    }
}
