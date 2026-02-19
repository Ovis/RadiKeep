using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using RadiKeep.Logics.Domain.Station;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.RecordJobLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Options;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Tests.Mocks;

namespace RadiKeep.Logics.Tests.LogicTest;

/// <summary>
/// StartupTaskのテスト
/// </summary>
public class StartupTaskTests
{
    /// <summary>
    /// FFmpeg未導入時は例外と通知
    /// </summary>
    [Test]
    public async Task InitializeAsync_Ffmpeg未導入_例外と通知()
    {
        var (task, repo) = CreateTarget(ffmpegOk: false);

        Assert.ThrowsAsync<DomainException>(async () => await task.InitializeAsync());

        var list = await repo.GetUnreadListAsync();
        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(list[0].Message, Does.Contain("ffmpeg"));
        Assert.That(list[1].Message, Does.Contain("ログを確認"));
    }

    /// <summary>
    /// 正常系で通知が増えない
    /// </summary>
    [Test]
    public async Task InitializeAsync_正常完了_通知なし()
    {
        var (task, repo) = CreateTarget(ffmpegOk: true, hasRadikoStations: true, hasRadiruStations: true);

        Assert.DoesNotThrowAsync(async () => await task.InitializeAsync());

        var list = await repo.GetUnreadListAsync();
        Assert.That(list.Count, Is.EqualTo(0));
    }

    /// <summary>
    /// StartupTask構築
    /// </summary>
    private static (StartupTask Task, FakeNotificationRepository Repo) CreateTarget(
        bool ffmpegOk,
        bool hasRadikoStations = true,
        bool hasRadiruStations = true)
    {
        var configMock = CreateConfig();

        var ffmpegMock = new Mock<IFfmpegService>();
        ffmpegMock.Setup(x => x.Initialize()).Returns(ffmpegOk);
        var radikoHttpHandler = new FakeHttpMessageHandler();
        radikoHttpHandler.AddHandler(
            req => req.RequestUri!.ToString().Contains("member/login"),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"paid_member\":\"0\",\"areafree\":\"0\",\"radiko_session\":\"\"}")
            });
        var radikoHttpClientFactory = new FakeHttpClientFactory(new HttpClient(radikoHttpHandler));

        var radikoLogic = new RadikoUniqueProcessLogic(
            new Mock<ILogger<RadikoUniqueProcessLogic>>().Object,
            configMock.Object,
            radikoHttpClientFactory);

        var schedulerFactory = CreateSchedulerFactory();
        var appContext = new FakeRadioAppContext();
        var recordJobLogic = new RecordJobLobLogic(
            new Mock<ILogger<RecordJobLobLogic>>().Object,
            schedulerFactory.Object,
            configMock.Object,
            appContext);

        var programScheduleRepo = new FakeProgramScheduleRepository();
        var programScheduleLogic = new ProgramScheduleLobLogic(
            new Mock<ILogger<ProgramScheduleLobLogic>>().Object,
            appContext,
            new FakeRadikoApiClient(),
            new FakeRadiruApiClient(),
            programScheduleRepo,
            recordJobLogic,
            new EntryMapper(configMock.Object));

        var stationRepoMock = new Mock<IStationRepository>();
        stationRepoMock.Setup(x => x.HasAnyRadikoStationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRadikoStations);
        stationRepoMock.Setup(x => x.GetRadikoStationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RadikoStation { StationId = "TBS", StationName = "TBS" }]);
        stationRepoMock.Setup(x => x.HasAnyRadiruStationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRadiruStations);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new HttpClientHandler()));

        var stationLogic = new StationLobLogic(
            new Mock<ILogger<StationLobLogic>>().Object,
            configMock.Object,
            new FakeRadikoApiClient(),
            stationRepoMock.Object,
            radikoLogic,
            httpClientFactoryMock.Object,
            new EntryMapper(configMock.Object));

        var notificationRepo = new FakeNotificationRepository();
        var notificationHttpClientFactory = new FakeHttpClientFactory(new HttpClient(new FakeHttpMessageHandler()));
        var notificationLogic = new NotificationLobLogic(
            new Mock<ILogger<NotificationLobLogic>>().Object,
            appContext,
            configMock.Object,
            new EntryMapper(configMock.Object),
            notificationHttpClientFactory,
            notificationRepo);
        var logMaintenanceLobLogic = new LogMaintenanceLobLogic(
            new Mock<ILogger<LogMaintenanceLobLogic>>().Object,
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        var dbPath = Path.Combine(Path.GetTempPath(), $"radiokeep-startup-tests-{Guid.NewGuid():N}.db");
        var dbOptions = new DbContextOptionsBuilder<RadioDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var dbContext = new RadioDbContext(dbOptions);
        dbContext.Database.EnsureCreated();
        var temporaryStorageMaintenanceLobLogic = new TemporaryStorageMaintenanceLobLogic(
            new Mock<ILogger<TemporaryStorageMaintenanceLobLogic>>().Object,
            configMock.Object,
            dbContext);
        var storageCapacityMonitorLobLogic = new StorageCapacityMonitorLobLogic(
            new Mock<ILogger<StorageCapacityMonitorLobLogic>>().Object,
            configMock.Object,
            notificationLogic);

        var task = new StartupTask(
            new Mock<ILogger<StartupTask>>().Object,
            configMock.Object,
            ffmpegMock.Object,
            radikoLogic,
            programScheduleLogic,
            logMaintenanceLobLogic,
            temporaryStorageMaintenanceLobLogic,
            storageCapacityMonitorLobLogic,
            stationLogic,
            notificationLogic);

        return (task, notificationRepo);
    }

    /// <summary>
    /// 設定モック
    /// </summary>
    private static Mock<IAppConfigurationService> CreateConfig()
    {
        var mock = new Mock<IAppConfigurationService>();

        mock.SetupGet(x => x.RadikoOptions).Returns(new RadikoOptions());
        mock.SetupGet(x => x.RecordStartDuration).Returns(TimeSpan.FromSeconds(5));
        mock.SetupGet(x => x.RecordEndDuration).Returns(TimeSpan.FromSeconds(5));
        mock.SetupGet(x => x.RecordFileSaveDir).Returns(Path.GetTempPath());
        mock.SetupGet(x => x.TemporaryFileSaveDir).Returns(Path.GetTempPath());
        mock.SetupGet(x => x.DiscordWebhookUrl).Returns(string.Empty);
        mock.SetupGet(x => x.StorageLowSpaceThresholdMb).Returns(1024);
        mock.SetupGet(x => x.StorageLowSpaceNotificationCooldownHours).Returns(24);
        mock.SetupGet(x => x.StorageLowSpaceCheckIntervalMinutes).Returns(30);
        mock.SetupGet(x => x.LogRetentionDays).Returns(100);
        mock.SetupGet(x => x.ExternalImportFileTimeZoneId).Returns(TimeZoneInfo.Local.Id);
        mock.SetupGet(x => x.NoticeCategories).Returns([]);
        mock.SetupGet(x => x.RadikoStationDic)
            .Returns(new System.Collections.Concurrent.ConcurrentDictionary<string, string>(
                new[] { new KeyValuePair<string, string>("TBS", "TBS") }));

        mock.Setup(x => x.UpdateRadikoPremiumUser(It.IsAny<bool>()));
        mock.Setup(x => x.UpdateRadikoAreaFree(It.IsAny<bool>()));
        mock.Setup(x => x.UpdateRadikoStationDic(It.IsAny<List<RadikoStation>>()));
        mock.Setup(x => x.TryGetRadikoCredentialsAsync())
            .Returns(ValueTask.FromResult((false, string.Empty, string.Empty)));

        return mock;
    }

    /// <summary>
    /// スケジューラのモック
    /// </summary>
    private static Mock<ISchedulerFactory> CreateSchedulerFactory()
    {
        var schedulerMock = new Mock<IScheduler>();
        schedulerMock.Setup(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        schedulerMock.Setup(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        schedulerMock.Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow);

        var factory = new Mock<ISchedulerFactory>();
        factory.Setup(x => x.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedulerMock.Object);
        return factory;
    }
}
