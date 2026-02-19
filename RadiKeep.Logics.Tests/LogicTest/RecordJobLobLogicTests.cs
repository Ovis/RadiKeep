using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Logics.RecordJobLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Tests.LogicTest
{

    [TestFixture]
    public class RecordJobLobLogicTests
    {
        private RecordJobLobLogic _recordJobLogic = null!;
        private Mock<IScheduler> _schedulerMock = null!;
        private Mock<IAppConfigurationService> _configServiceMock = null!;

        [SetUp]
        public void Setup()
        {
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            _schedulerMock = new Mock<IScheduler>();
            var contextMock = new Mock<IRadioAppContext>();
            _configServiceMock = new Mock<IAppConfigurationService>();

            _configServiceMock.SetupGet(x => x.RecordStartDuration).Returns(TimeSpan.FromSeconds(15));
            _configServiceMock.SetupGet(x => x.RecordEndDuration).Returns(TimeSpan.FromSeconds(30));

            schedulerFactoryMock.Setup(
                    x => x.GetScheduler(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_schedulerMock.Object));

            _schedulerMock.Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(DateTimeOffset.UtcNow));
            _schedulerMock.Setup(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            _schedulerMock.Setup(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            _recordJobLogic = new RecordJobLobLogic(
                new Mock<ILogger<RecordJobLobLogic>>().Object,
                schedulerFactoryMock.Object,
                _configServiceMock.Object,
                contextMock.Object);
        }

        [Test]
        public async Task SetScheduleJobAsync_リアルタイム予約_設定画面マージンが適用される()
        {
            // Arrange
            var job = new ScheduleJob
            {
                Id = Ulid.NewUlid(),
                ServiceKind = RadioServiceKind.Radiko,
                StationId = "TBS",
                ProgramId = "TBS_MARGIN_GLOBAL_001",
                FilePath = "",
                StartDateTime = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                EndDateTime = new DateTime(2030, 1, 2, 4, 4, 5, DateTimeKind.Utc),
                Title = "GlobalMargin",
                RecordingType = RecordingType.RealTime,
                ReserveType = ReserveType.Program,
                IsEnabled = true,
                StartDelay = null,
                EndDelay = null
            };

            IJobDetail? capturedJobDetail = null;
            ITrigger? capturedTrigger = null;
            _schedulerMock
                .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .Callback<IJobDetail, ITrigger, CancellationToken>((detail, trigger, _) =>
                {
                    capturedJobDetail = detail;
                    capturedTrigger = trigger;
                })
                .Returns(Task.FromResult(DateTimeOffset.UtcNow));

            // Act
            var result = await _recordJobLogic.SetScheduleJobAsync(job);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(capturedJobDetail, Is.Not.Null);
            Assert.That(capturedTrigger, Is.Not.Null);
            Assert.That(Convert.ToDouble(capturedJobDetail!.JobDataMap["startDelay"]), Is.EqualTo(15d));
            Assert.That(Convert.ToDouble(capturedJobDetail!.JobDataMap["endDelay"]), Is.EqualTo(30d));

            var expectedStart = job.StartDateTime.AddSeconds(-16);
            Assert.That(capturedTrigger!.StartTimeUtc, Is.EqualTo(expectedStart));
        }

        [Test]
        public async Task SetScheduleJobAsync_リアルタイム予約_キーワード予約マージンが優先される()
        {
            // Arrange
            var job = new ScheduleJob
            {
                Id = Ulid.NewUlid(),
                ServiceKind = RadioServiceKind.Radiko,
                StationId = "TBS",
                ProgramId = "TBS_MARGIN_KEYWORD_001",
                FilePath = "",
                StartDateTime = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                EndDateTime = new DateTime(2030, 1, 2, 4, 4, 5, DateTimeKind.Utc),
                Title = "KeywordMargin",
                RecordingType = RecordingType.RealTime,
                ReserveType = ReserveType.Keyword,
                IsEnabled = true,
                StartDelay = TimeSpan.FromSeconds(40),
                EndDelay = TimeSpan.FromSeconds(50)
            };

            IJobDetail? capturedJobDetail = null;
            ITrigger? capturedTrigger = null;
            _schedulerMock
                .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .Callback<IJobDetail, ITrigger, CancellationToken>((detail, trigger, _) =>
                {
                    capturedJobDetail = detail;
                    capturedTrigger = trigger;
                })
                .Returns(Task.FromResult(DateTimeOffset.UtcNow));

            // Act
            var result = await _recordJobLogic.SetScheduleJobAsync(job);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(capturedJobDetail, Is.Not.Null);
            Assert.That(capturedTrigger, Is.Not.Null);
            Assert.That(Convert.ToDouble(capturedJobDetail!.JobDataMap["startDelay"]), Is.EqualTo(40d));
            Assert.That(Convert.ToDouble(capturedJobDetail!.JobDataMap["endDelay"]), Is.EqualTo(50d));

            var expectedStart = job.StartDateTime.AddSeconds(-41);
            Assert.That(capturedTrigger!.StartTimeUtc, Is.EqualTo(expectedStart));
        }

        [Test]
        public async Task SetScheduleJobAsync_ValidJob_ReturnsSuccess()
        {
            // Arrange
            var job = new ScheduleJob
            {
                Id = Ulid.NewUlid(),
                ServiceKind = RadioServiceKind.Radiko,
                StationId = "TBS",
                ProgramId = "TBS_1234567890123456789012345678",
                FilePath = "",
                StartDateTime = DateTime.Today.AddDays(3),
                EndDateTime = DateTime.Today.AddDays(5),
                Title = "TestTitle",
                RecordingType = RecordingType.TimeFree,
                ReserveType = ReserveType.Program,
                IsEnabled = true
            };

            // Act
            var result = await _recordJobLogic.SetScheduleJobAsync(job);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
        }

        [Test]
        public async Task SetScheduleJobsAsync_ValidJobs_ReturnsSuccess()
        {
            // Arrange
            List<ScheduleJob> jobs =
            [
                new()
                {
                    Id = Ulid.NewUlid(),
                    ServiceKind = RadioServiceKind.Radiko,
                    StationId = "TBS",
                    ProgramId = "TBS_1234567890123456789012345678",
                    FilePath = "",
                    StartDateTime = DateTime.Today.AddDays(3),
                    EndDateTime = DateTime.Today.AddDays(4),
                    Title = "TestTitle",
                    RecordingType = RecordingType.TimeFree,
                    ReserveType = ReserveType.Program,
                    IsEnabled = true
                },

                new()
                {
                    Id = Ulid.NewUlid(),
                    ServiceKind = RadioServiceKind.Radiko,
                    StationId = "CBC",
                    ProgramId = "CBC_9876543210123456789012345678",
                    FilePath = "",
                    StartDateTime = DateTime.Today.AddDays(4),
                    EndDateTime = DateTime.Today.AddDays(5),
                    Title = "TestTitle2",
                    RecordingType = RecordingType.TimeFree,
                    ReserveType = ReserveType.Program,
                    IsEnabled = true
                }
            ];

            // Act
            var result = await _recordJobLogic.SetScheduleJobsAsync(jobs);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.Null);
        }

        [Test]
        public async Task DeleteScheduleJobAsync_ValidJobId_ReturnsSuccess()
        {
            var job = new ScheduleJob
            {
                Id = Ulid.NewUlid(),
                ServiceKind = RadioServiceKind.Radiko,
                StationId = "TBS",
                ProgramId = "TBS_1234567890123456789012345678",
                FilePath = "",
                StartDateTime = DateTime.Today.AddDays(3),
                EndDateTime = DateTime.Today.AddDays(5),
                Title = "TestTitle",
                RecordingType = RecordingType.TimeFree,
                ReserveType = ReserveType.Program,
                IsEnabled = true
            };

            // データ登録
            {
                // Act
                var (isSuccess, error) = await _recordJobLogic.SetScheduleJobAsync(job);

                if (!isSuccess)
                {
                    Assert.Fail(error?.Message ?? string.Empty);
                }
            }

            // Act
            var (b, exception) = await _recordJobLogic.DeleteScheduleJobAsync(job.Id);

            // Assert
            Assert.That(b, Is.True);
            Assert.That(exception, Is.Null);
        }

        [Test]
        public async Task DeleteScheduleJobsAsync_ValidJobs_ReturnsSuccess()
        {
            // Arrange
            List<ScheduleJob> jobs =
            [
                new()
                {
                    Id = Ulid.NewUlid(),
                    ServiceKind = RadioServiceKind.Radiko,
                    StationId = "TBS",
                    ProgramId = "TBS_1234567890123456789012345678",
                    FilePath = "",
                    StartDateTime = DateTime.Today.AddDays(3),
                    EndDateTime = DateTime.Today.AddDays(4),
                    Title = "TestTitle",
                    RecordingType = RecordingType.TimeFree,
                    ReserveType = ReserveType.Program,
                    IsEnabled = true
                },

                new()
                {
                    Id = Ulid.NewUlid(),
                    ServiceKind = RadioServiceKind.Radiko,
                    StationId = "CBC",
                    ProgramId = "CBC_9876543210123456789012345678",
                    FilePath = "",
                    StartDateTime = DateTime.Today.AddDays(4),
                    EndDateTime = DateTime.Today.AddDays(5),
                    Title = "TestTitle2",
                    RecordingType = RecordingType.TimeFree,
                    ReserveType = ReserveType.Program,
                    IsEnabled = true
                }
            ];

            // データ登録
            {
                // Act
                var (isSuccess, error) = await _recordJobLogic.SetScheduleJobsAsync(jobs);

                if (!isSuccess)
                {
                    Assert.Fail(error?.Message ?? string.Empty);
                }
            }

            // Act
            var (b, exception) = await _recordJobLogic.DeleteScheduleJobsAsync(jobs);

            // Assert
            Assert.That(b, Is.True);
            Assert.That(exception, Is.Null);
        }
    }
}
