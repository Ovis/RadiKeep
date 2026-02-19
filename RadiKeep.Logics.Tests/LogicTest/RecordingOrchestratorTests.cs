using Microsoft.Extensions.Logging;
using Moq;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Tests.Mocks;
using RadiKeep.Logics.UseCases.Recording;

namespace RadiKeep.Logics.Tests.LogicTest;

public class RecordingOrchestratorTests
{
    /// <summary>
    /// 正常系: 録音が成功し、状態がCompletedになること
    /// </summary>
    [Test]
    public async Task RecordAsync_Success_ReturnsCompleted()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var metadata = new ProgramRecordingInfo(
            ProgramId: "P1",
            Title: "Test",
            Subtitle: "",
            StationId: "ST",
            StationName: "Station",
            AreaId: "AR",
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow.AddMinutes(30),
            Performer: "P",
            Description: "D",
            ProgramUrl: "");

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var sourceResult = new RecordingSourceResult(
            StreamUrl: "http://example/stream.m3u8",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: metadata,
            Options: options);

        var source = new FakeRecordingSource(RadioServiceKind.Radiko, sourceResult);
        var storage = new FakeMediaStorageService();
        var transcoder = new FakeMediaTranscodeService(true);
        var repo = new InMemoryRecordingRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            new[] { source },
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P1",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.RecordingId, Is.Not.Null);
        Assert.That(storage.IsCommitCalled, Is.True);
        Assert.That(storage.IsCleanupCalled, Is.False);

        var id = result.RecordingId!.Value;
        Assert.That(repo.Store[id].State, Is.EqualTo(RecordingState.Completed));
    }

    /// <summary>
    /// 正常系: コミット時に最終パスが変更された場合、リポジトリへ変更後パスが反映されること
    /// </summary>
    [Test]
    public async Task RecordAsync_Commitでリネーム_変更後パスが保存される()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var metadata = new ProgramRecordingInfo(
            ProgramId: "P1",
            Title: "Test",
            Subtitle: "",
            StationId: "ST",
            StationName: "Station",
            AreaId: "AR",
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow.AddMinutes(30),
            Performer: "P",
            Description: "D",
            ProgramUrl: "");

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var sourceResult = new RecordingSourceResult(
            StreamUrl: "http://example/stream.m3u8",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: metadata,
            Options: options);

        var source = new FakeRecordingSource(RadioServiceKind.Radiko, sourceResult);
        var storage = new RenameOnCommitMediaStorageService(
            prepared: new MediaPath("temp.m4a", "record/Test.m4a", "record/Test.m4a"),
            committed: new MediaPath("temp.m4a", "record/Test_duplicate_123.m4a", "record/Test_duplicate_123.m4a"));
        var transcoder = new FakeMediaTranscodeService(true);
        var repo = new InMemoryRecordingRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            new[] { source },
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P1",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.RecordingId, Is.Not.Null);

        var id = result.RecordingId!.Value;
        Assert.That(repo.Store[id].Path.FinalFilePath, Is.EqualTo("record/Test_duplicate_123.m4a"));
        Assert.That(repo.Store[id].Path.RelativePath, Is.EqualTo("record/Test_duplicate_123.m4a"));
    }

    /// <summary>
    /// 異常系: 録音失敗時に状態がFailedになること
    /// </summary>
    [Test]
    public async Task RecordAsync_TranscodeFail_ReturnsFailed()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var metadata = new ProgramRecordingInfo(
            ProgramId: "P2",
            Title: "Test",
            Subtitle: "",
            StationId: "ST",
            StationName: "Station",
            AreaId: "AR",
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow.AddMinutes(30),
            Performer: "P",
            Description: "D",
            ProgramUrl: "");

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var sourceResult = new RecordingSourceResult(
            StreamUrl: "http://example/stream.m3u8",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: metadata,
            Options: options);

        var source = new FakeRecordingSource(RadioServiceKind.Radiko, sourceResult);
        var storage = new FakeMediaStorageService();
        var transcoder = new FakeMediaTranscodeService(false);
        var repo = new InMemoryRecordingRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            new[] { source },
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P2",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.RecordingId, Is.Not.Null);
        Assert.That(storage.IsCommitCalled, Is.False);
        Assert.That(storage.IsCleanupCalled, Is.True);

        var id = result.RecordingId!.Value;
        Assert.That(repo.Store[id].State, Is.EqualTo(RecordingState.Failed));
    }

    /// <summary>
    /// 異常系: 対応ソースが存在しない場合は失敗を返す
    /// </summary>
    [Test]
    public async Task RecordAsync_NoSource_ReturnsFailed()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var storage = new FakeMediaStorageService();
        var transcoder = new FakeMediaTranscodeService(true);
        var repo = new InMemoryRecordingRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            Array.Empty<IRecordingSource>(),
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P3",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.RecordingId, Is.Null);
        Assert.That(storage.IsCommitCalled, Is.False);
        Assert.That(storage.IsCleanupCalled, Is.False);
    }

    /// <summary>
    /// 異常系: ソース準備が失敗した場合は失敗を返す
    /// </summary>
    [Test]
    public async Task RecordAsync_PrepareThrows_ReturnsFailed()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var metadata = new ProgramRecordingInfo(
            ProgramId: "P4",
            Title: "Test",
            Subtitle: "",
            StationId: "ST",
            StationName: "Station",
            AreaId: "AR",
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow.AddMinutes(30),
            Performer: "P",
            Description: "D",
            ProgramUrl: "");

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var sourceResult = new RecordingSourceResult(
            StreamUrl: "http://example/stream.m3u8",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: metadata,
            Options: options);

        var source = new FakeRecordingSource(
            RadioServiceKind.Radiko,
            sourceResult,
            new DomainException("準備に失敗しました。"));

        var storage = new FakeMediaStorageService();
        var transcoder = new FakeMediaTranscodeService(true);
        var repo = new InMemoryRecordingRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            new[] { source },
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P4",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.RecordingId, Is.Null);
        Assert.That(storage.IsCommitCalled, Is.False);
        Assert.That(storage.IsCleanupCalled, Is.False);
    }

    /// <summary>
    /// 異常系: Commit失敗時はCleanupが実行される
    /// </summary>
    [Test]
    public async Task RecordAsync_CommitThrows_Cleanup実行()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var metadata = new ProgramRecordingInfo(
            ProgramId: "P5",
            Title: "Test",
            Subtitle: "",
            StationId: "ST",
            StationName: "Station",
            AreaId: "AR",
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow.AddMinutes(30),
            Performer: "P",
            Description: "D",
            ProgramUrl: "");

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var sourceResult = new RecordingSourceResult(
            StreamUrl: "http://example/stream.m3u8",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: metadata,
            Options: options);

        var source = new FakeRecordingSource(RadioServiceKind.Radiko, sourceResult);
        var storage = new ThrowingMediaStorageService();
        var transcoder = new FakeMediaTranscodeService(true);
        var repo = new InMemoryRecordingRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            new[] { source },
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P5",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(storage.IsCleanupCalled, Is.True);
    }

    /// <summary>
    /// 異常系: 状態更新が失敗しても処理は継続する
    /// </summary>
    [Test]
    public async Task RecordAsync_UpdateStateThrows_処理継続()
    {
        var logger = new Mock<ILogger<RecordingOrchestrator>>().Object;

        var metadata = new ProgramRecordingInfo(
            ProgramId: "P6",
            Title: "Test",
            Subtitle: "",
            StationId: "ST",
            StationName: "Station",
            AreaId: "AR",
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow.AddMinutes(30),
            Performer: "P",
            Description: "D",
            ProgramUrl: "");

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var sourceResult = new RecordingSourceResult(
            StreamUrl: "http://example/stream.m3u8",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: metadata,
            Options: options);

        var source = new FakeRecordingSource(RadioServiceKind.Radiko, sourceResult);
        var storage = new FakeMediaStorageService();
        var transcoder = new FakeMediaTranscodeService(false);
        var repo = new ThrowingUpdateStateRepository();

        var orchestrator = new RecordingOrchestrator(
            logger,
            new[] { source },
            storage,
            transcoder,
            repo);

        var command = new RecordingCommand(
            ServiceKind: RadioServiceKind.Radiko,
            ProgramId: "P6",
            ProgramName: "Test",
            IsTimeFree: false,
            StartDelaySeconds: 0,
            EndDelaySeconds: 0);

        var result = await orchestrator.RecordAsync(command);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.RecordingId, Is.Not.Null);
    }

    private sealed class ThrowingMediaStorageService : IMediaStorageService
    {
        public bool IsCleanupCalled { get; private set; }

        public ValueTask<MediaPath> PrepareAsync(ProgramRecordingInfo programInfo, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new MediaPath("temp.m4a", "final.m4a", "rel\\final.m4a"));

        public ValueTask<MediaPath> CommitAsync(MediaPath path, CancellationToken cancellationToken = default)
            => throw new Exception("commit fail");

        public ValueTask CleanupTempAsync(MediaPath path, CancellationToken cancellationToken = default)
        {
            IsCleanupCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingUpdateStateRepository : IRecordingRepository
    {
        public ValueTask<Ulid> CreateAsync(ProgramRecordingInfo programInfo, MediaPath path, RecordingOptions options, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Ulid.NewUlid());

        public ValueTask UpdateStateAsync(Ulid recordingId, RecordingState state, string? errorMessage = null, CancellationToken cancellationToken = default)
            => throw new Exception("update fail");

        public ValueTask UpdateFilePathAsync(Ulid recordingId, MediaPath path, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class RenameOnCommitMediaStorageService(MediaPath prepared, MediaPath committed) : IMediaStorageService
    {
        public ValueTask<MediaPath> PrepareAsync(ProgramRecordingInfo programInfo, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(prepared);

        public ValueTask<MediaPath> CommitAsync(MediaPath path, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(committed);

        public ValueTask CleanupTempAsync(MediaPath path, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
