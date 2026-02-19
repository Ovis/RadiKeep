using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Moq;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Infrastructure.Recording;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Tests.LogicTest;

/// <summary>
/// MediaTranscodeServiceのテスト
/// </summary>
public class MediaTranscodeServiceTests
{
    /// <summary>
    /// タイムフリー録音はradiko以外は失敗
    /// </summary>
    [Test]
    public async Task RecordAsync_TimeFreeNonRadiko_失敗()
    {
        var logger = new Mock<ILogger<MediaTranscodeService>>().Object;
        var ffmpeg = new Mock<IFfmpegService>();
        var config = CreateConfigMock();

        var service = new MediaTranscodeService(logger, ffmpeg.Object, config.Object);

        var source = new RecordingSourceResult(
            StreamUrl: "http://example",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: CreateProgramInfo(),
            Options: new RecordingOptions(RadioServiceKind.Radiru, true, 0, 0));

        var path = new MediaPath("temp.m4a", "final.m4a", string.Empty);
        var result = await service.RecordAsync(source, path);

        Assert.That(result, Is.False);
        ffmpeg.Verify(x => x.RunProcessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// タイムフリー録音URLが空なら失敗
    /// </summary>
    [Test]
    public async Task RecordAsync_TimeFreeEmptyUrl_失敗()
    {
        var logger = new Mock<ILogger<MediaTranscodeService>>().Object;
        var ffmpeg = new Mock<IFfmpegService>();
        var config = CreateConfigMock();
        var service = new MediaTranscodeService(logger, ffmpeg.Object, config.Object);

        var source = new RecordingSourceResult(
            StreamUrl: "",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: CreateProgramInfo(),
            Options: new RecordingOptions(RadioServiceKind.Radiko, true, 0, 0));

        var result = await service.RecordAsync(source, new MediaPath("temp.m4a", "final.m4a", string.Empty));

        Assert.That(result, Is.False);
        ffmpeg.Verify(x => x.RunProcessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// タイムフリー録音の開始/終了が不正なら失敗
    /// </summary>
    [Test]
    public async Task RecordAsync_TimeFreeInvalidRange_失敗()
    {
        var logger = new Mock<ILogger<MediaTranscodeService>>().Object;
        var ffmpeg = new Mock<IFfmpegService>();
        var config = CreateConfigMock();
        var service = new MediaTranscodeService(logger, ffmpeg.Object, config.Object);

        var program = CreateProgramInfo();
        program = program with { EndTime = program.StartTime.AddSeconds(-1) };

        var source = new RecordingSourceResult(
            StreamUrl: "http://example",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: program,
            Options: new RecordingOptions(RadioServiceKind.Radiko, true, 0, 0));

        var result = await service.RecordAsync(source, new MediaPath("temp.m4a", "final.m4a", string.Empty));

        Assert.That(result, Is.False);
        ffmpeg.Verify(x => x.RunProcessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// タイムフリー録音が成功する
    /// </summary>
    [Test]
    public async Task RecordAsync_TimeFreeSuccess_チャンクと結合()
    {
        var logger = new Mock<ILogger<MediaTranscodeService>>().Object;
        var ffmpeg = new Mock<IFfmpegService>();

        var args = new ConcurrentQueue<string>();
        ffmpeg.Setup(x => x.RunProcessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback<string, int, string, CancellationToken>((a, _, _, _) => args.Enqueue(a));
        var config = CreateConfigMock();

        var service = new MediaTranscodeService(logger, ffmpeg.Object, config.Object);

        var program = CreateProgramInfo();
        program = program with
        {
            StartTime = new DateTimeOffset(2026, 2, 9, 12, 0, 0, TimeSpan.FromHours(9)),
            EndTime = new DateTimeOffset(2026, 2, 9, 12, 0, 10, TimeSpan.FromHours(9))
        };

        var source = new RecordingSourceResult(
            StreamUrl: "http://example/playlist",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: program,
            Options: new RecordingOptions(RadioServiceKind.Radiko, true, 0, 0));

        var path = new MediaPath(Path.Combine(Path.GetTempPath(), "temp.m4a"), "final.m4a", string.Empty);

        var result = await service.RecordAsync(source, path);

        Assert.That(result, Is.True);
        Assert.That(args.Count, Is.EqualTo(2));

        var first = args.ElementAt(0);
        var second = args.ElementAt(1);

        Assert.That(first, Does.Contain("type=c"));
        Assert.That(second, Does.Contain("-f concat"));
        Assert.That(second, Does.Contain(path.TempFilePath));
    }

    /// <summary>
    /// リアルタイム録音（Radiru）はfaststartを付与する
    /// </summary>
    [Test]
    public async Task RecordAsync_RealTimeRadiru_高速再生フラグ()
    {
        var logger = new Mock<ILogger<MediaTranscodeService>>().Object;
        var ffmpeg = new Mock<IFfmpegService>();
        var config = CreateConfigMock();
        string? captured = null;

        ffmpeg.Setup(x => x.RunProcessAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback<string, int, string, CancellationToken>((a, _, _, _) => captured = a);

        var service = new MediaTranscodeService(logger, ffmpeg.Object, config.Object);

        var program = CreateProgramInfo();
        program = program with
        {
            StartTime = DateTimeOffset.Now.AddMinutes(-1),
            EndTime = DateTimeOffset.Now.AddMinutes(1)
        };

        var source = new RecordingSourceResult(
            StreamUrl: "http://example/stream",
            Headers: new Dictionary<string, string>(),
            ProgramInfo: program,
            Options: new RecordingOptions(RadioServiceKind.Radiru, false, 0, 0));

        var path = new MediaPath(Path.Combine(Path.GetTempPath(), "temp.m4a"), "final.m4a", string.Empty);

        var result = await service.RecordAsync(source, path);

        Assert.That(result, Is.True);
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!, Does.Contain("-movflags +faststart"));
        Assert.That(captured!, Does.Contain(path.TempFilePath));
    }

    /// <summary>
    /// テスト用番組情報
    /// </summary>
    private static ProgramRecordingInfo CreateProgramInfo()
        => new(
            ProgramId: "P1",
            Title: "TestTitle",
            Subtitle: "Sub",
            StationId: "TBS",
            StationName: "TBS",
            AreaId: "JP13",
            StartTime: new DateTimeOffset(2026, 2, 9, 12, 0, 0, TimeSpan.FromHours(9)),
            EndTime: new DateTimeOffset(2026, 2, 9, 12, 30, 0, TimeSpan.FromHours(9)),
            Performer: "Tester",
            Description: "Desc",
            ProgramUrl: "http://example");

    /// <summary>
    /// 設定モック
    /// </summary>
    private static Mock<IAppConfigurationService> CreateConfigMock()
    {
        var mock = new Mock<IAppConfigurationService>();
        var tempDir = Path.Combine(Path.GetTempPath(), "radikeep-tests-temp");
        Directory.CreateDirectory(tempDir);

        mock.SetupGet(x => x.TemporaryFileSaveDir).Returns(tempDir);
        return mock;
    }
}
