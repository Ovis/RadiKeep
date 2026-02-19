using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
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
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Tests.Mocks;

namespace RadiKeep.Logics.Tests.LogicTest;

public class RadiruRecordingSourceTests
{
    private static ProgramScheduleLobLogic CreateProgramScheduleLobLogic(
        IProgramScheduleRepository repository,
        IEntryMapper entryMapper)
    {
        var logger = new Mock<ILogger<ProgramScheduleLobLogic>>().Object;
        var appContext = new FakeRadioAppContext();
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

    private static RadiruRecordingSource CreateTarget(NhkRadiruProgram? program, NhkRadiruStation station)
    {
        var configMock = new Mock<IAppConfigurationService>();
        var entryMapper = new EntryMapper(configMock.Object);
        var repository = new FakeProgramScheduleRepository
        {
            RadikoProgramById = null
        };

        var repoMock = new Mock<IProgramScheduleRepository>();
        repoMock.Setup(r => r.GetRadiruProgramByIdAsync("R1_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(program);

        var programScheduleLogic = CreateProgramScheduleLobLogic(repoMock.Object, entryMapper);

        var stationRepository = new FakeStationRepository { RadiruStation = station };
        var radikoApiClient = new FakeRadikoApiClient();
        var radikoHttpClientFactory = new FakeHttpClientFactory(new HttpClient(new FakeHttpMessageHandler()));
        var radikoLogic = new RadikoUniqueProcessLogic(
            new Mock<ILogger<RadikoUniqueProcessLogic>>().Object,
            configMock.Object,
            radikoHttpClientFactory);
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

        return new RadiruRecordingSource(
            new Mock<ILogger<RadiruRecordingSource>>().Object,
            programScheduleLogic,
            stationLogic);
    }

    [Test]
    public void PrepareAsync_番組がない場合は失敗()
    {
        var station = new NhkRadiruStation();
        var target = CreateTarget(null, station);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiru,
            ProgramId: "R1_1",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var ex = Assert.ThrowsAsync<DomainException>(async () => await target.PrepareAsync(command));
        Assert.That(ex!.Message, Is.EqualTo("番組情報の取得に失敗しました。"));
    }

    [Test]
    public void PrepareAsync_放送終了済みは失敗()
    {
        var area = RadiruAreaKind.東京.GetEnumCodeId();
        var program = new NhkRadiruProgram
        {
            ProgramId = "R1_1",
            StationId = "r1",
            AreaId = area,
            Title = "NHK",
            Subtitle = "Test",
            RadioDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DaysOfWeek = DaysOfWeek.Monday,
            StartTime = DateTimeOffset.Now.AddMinutes(-60),
            EndTime = DateTimeOffset.Now.AddMinutes(-30),
            EventId = "EV",
            SiteId = "site",
            ProgramUrl = "http://example"
        };

        var station = new NhkRadiruStation
        {
            AreaId = area,
            R1Hls = "http://r1"
        };

        var target = CreateTarget(program, station);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiru,
            ProgramId: "R1_1",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var ex = Assert.ThrowsAsync<DomainException>(async () => await target.PrepareAsync(command));
        Assert.That(ex!.Message, Is.EqualTo("番組が放送終了しているため録音できません。"));
    }

    [Test]
    public async Task PrepareAsync_R1のHlsを返す()
    {
        var area = RadiruAreaKind.東京.GetEnumCodeId();
        var program = new NhkRadiruProgram
        {
            ProgramId = "R1_1",
            StationId = "r1",
            AreaId = area,
            Title = "NHK",
            Subtitle = "Test",
            RadioDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DaysOfWeek = DaysOfWeek.Monday,
            StartTime = DateTimeOffset.Now.AddMinutes(10),
            EndTime = DateTimeOffset.Now.AddMinutes(40),
            EventId = "EV",
            SiteId = "site",
            ProgramUrl = "http://example"
        };

        var station = new NhkRadiruStation
        {
            AreaId = area,
            R1Hls = "http://r1-hls"
        };

        var target = CreateTarget(program, station);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiru,
            ProgramId: "R1_1",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await target.PrepareAsync(command);

        Assert.That(result.StreamUrl, Is.EqualTo("http://r1-hls"));
        Assert.That(result.Options.ServiceKind, Is.EqualTo(RadioServiceKind.Radiru));
        Assert.That(result.Options.IsTimeFree, Is.False);
    }
}
