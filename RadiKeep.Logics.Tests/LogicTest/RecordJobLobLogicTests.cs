using Microsoft.Extensions.Logging;
using Moq;
using RadiKeep.Logics.Logics.RecordJobLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Tests.LogicTest;

[TestFixture]
public class RecordJobLobLogicTests
{
    private RecordJobLobLogic _recordJobLogic = null!;

    [SetUp]
    public void Setup()
    {
        var configServiceMock = new Mock<IAppConfigurationService>();
        configServiceMock.SetupGet(x => x.RecordStartDuration).Returns(TimeSpan.FromSeconds(15));
        configServiceMock.SetupGet(x => x.RecordEndDuration).Returns(TimeSpan.FromSeconds(30));

        _recordJobLogic = new RecordJobLobLogic(
            new Mock<ILogger<RecordJobLobLogic>>().Object,
            configServiceMock.Object);
    }

    [Test]
    public async Task SetScheduleJobAsync_有効なジョブで成功する()
    {
        var job = new ScheduleJob
        {
            Id = Ulid.NewUlid(),
            ServiceKind = RadioServiceKind.Radiko,
            StationId = "TBS",
            ProgramId = "P1",
            StartDateTime = DateTimeOffset.UtcNow.AddMinutes(5),
            EndDateTime = DateTimeOffset.UtcNow.AddMinutes(35),
            Title = "TestTitle",
            RecordingType = RecordingType.RealTime,
            ReserveType = ReserveType.Program,
            IsEnabled = true
        };

        var result = await _recordJobLogic.SetScheduleJobAsync(job);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public async Task DeleteScheduleJobAsync_存在しないジョブでも成功する()
    {
        var result = await _recordJobLogic.DeleteScheduleJobAsync(Ulid.NewUlid());

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Error, Is.Null);
    }

}
