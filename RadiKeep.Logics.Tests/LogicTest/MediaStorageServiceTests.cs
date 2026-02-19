using Moq;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Infrastructure.Recording;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Tests.LogicTest;

/// <summary>
/// MediaStorageServiceのテスト
/// </summary>
public class MediaStorageServiceTests
{
    /// <summary>
    /// テンプレート指定時にパスが正しく構成される
    /// </summary>
    [Test]
    public async Task PrepareAsync_TemplateAndRelativePath_正しく構成される()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(
                baseDir,
                tempDir,
                recordDirectoryRelativePath: "$StationId$\\$SYYYY$",
                recordFileNameTemplate: "$Title$");

            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();

            var result = await service.PrepareAsync(program);

            var expectedRelative = Path.Combine("TBS", "2026", "TestTitle.m4a");
            var expectedFull = Path.Combine(baseDir, expectedRelative);

            Assert.That(result.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(result.FinalFilePath, Is.EqualTo(expectedFull));
            Assert.That(Path.GetDirectoryName(result.TempFilePath), Is.EqualTo(Path.Combine(tempDir, TemporaryStoragePaths.RecordingsWorkDirectoryName)));
            Assert.That(Path.GetFileName(result.TempFilePath), Does.StartWith("P1_"));
            Assert.That(Path.GetExtension(result.TempFilePath), Is.EqualTo(".m4a"));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// 同一番組でも準備時の一時ファイル名は重複しない
    /// </summary>
    [Test]
    public async Task PrepareAsync_同一番組を連続実行_一時ファイルが重複しない()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(baseDir, tempDir, null, "$Title$");
            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();

            var first = await service.PrepareAsync(program);
            var second = await service.PrepareAsync(program);

            Assert.That(first.TempFilePath, Is.Not.EqualTo(second.TempFilePath));
            Assert.That(Path.GetFileName(first.TempFilePath), Does.StartWith("P1_"));
            Assert.That(Path.GetFileName(second.TempFilePath), Does.StartWith("P1_"));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// 保存先が不正な場合は例外
    /// </summary>
    [Test]
    public void PrepareAsync_保存先不正_例外()
    {
        var configMock = CreateConfig(
            recordFileSaveDir: "relative",
            temporaryFileSaveDir: "temp",
            recordDirectoryRelativePath: "folder",
            recordFileNameTemplate: "$Title$");

        var service = new MediaStorageService(configMock.Object);
        var program = CreateProgramInfo();

        Assert.ThrowsAsync<DomainException>(async () => await service.PrepareAsync(program));
    }

    /// <summary>
    /// 既存ファイルがある場合はリネーム保存する
    /// </summary>
    [Test]
    public async Task CommitAsync_既存ファイルあり_リネーム保存()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(
                baseDir,
                tempDir,
                recordDirectoryRelativePath: null,
                recordFileNameTemplate: "TestTitle");

            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();
            var path = await service.PrepareAsync(program);

            // 既存ファイルを作成
            Directory.CreateDirectory(Path.GetDirectoryName(path.FinalFilePath)!);
            File.WriteAllText(path.FinalFilePath, "old");

            // 一時ファイルを作成
            File.WriteAllText(path.TempFilePath, "new");

            await service.CommitAsync(path);

            Assert.That(File.Exists(path.TempFilePath), Is.False);
            Assert.That(File.Exists(path.FinalFilePath), Is.True);

            var files = Directory.GetFiles(Path.GetDirectoryName(path.FinalFilePath)!, "TestTitle_duplicate_*.m4a");
            Assert.That(files.Length, Is.EqualTo(1));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// 一時ファイルの削除
    /// </summary>
    [Test]
    public async Task CleanupTempAsync_一時ファイル削除()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(baseDir, tempDir, null, null);
            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();
            var path = await service.PrepareAsync(program);

            File.WriteAllText(path.TempFilePath, "temp");

            await service.CleanupTempAsync(path);

            Assert.That(File.Exists(path.TempFilePath), Is.False);
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// 全トークンが期待どおりに展開される
    /// </summary>
    [Test]
    public async Task PrepareAsync_全トークンを含むテンプレート_正しく展開される()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(
                baseDir,
                tempDir,
                recordDirectoryRelativePath: "$StationName$\\$EYYYY$\\$EMM$\\$EDD$",
                recordFileNameTemplate:
                "$StationId$_$Title$_$SYYYY$_$SYY$_$SMM$_$SM$_$SDD$_$SD$_$STHH$_$STH$_$STMM$_$STM$_$STSS$_$STS$_$EYYYY$_$EYY$_$EMM$_$EM$_$EDD$_$ED$_$ETHH$_$ETH$_$ETMM$_$ETM$_$ETSS$_$ETS$");

            var service = new MediaStorageService(configMock.Object);
            var program = new ProgramRecordingInfo(
                ProgramId: "P2",
                Title: "Title:/?A",
                Subtitle: "Sub",
                StationId: "ST:01",
                StationName: "Station/Name",
                AreaId: "JP13",
                StartTime: new DateTimeOffset(2026, 2, 9, 12, 3, 4, TimeSpan.FromHours(9)),
                EndTime: new DateTimeOffset(2026, 12, 31, 23, 45, 56, TimeSpan.FromHours(9)),
                Performer: "Tester",
                Description: "Desc",
                ProgramUrl: "http://example");

            var result = await service.PrepareAsync(program);
            var expectedFileName =
                "ST：01_Title：／？A_2026_26_02_2_09_9_12_12_03_3_04_4_2026_26_12_12_31_31_23_23_45_45_56_56.m4a";
            var expectedRelative = Path.Combine("Station／Name", "2026", "12", "31", expectedFileName);

            Assert.That(result.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(Path.GetFileName(result.FinalFilePath), Is.EqualTo(expectedFileName));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// ファイル名テンプレートが不正な場合は既定ファイル名へフォールバックする
    /// </summary>
    [Test]
    public async Task PrepareAsync_不正なファイル名テンプレート_既定ファイル名にフォールバックする()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(
                baseDir,
                tempDir,
                recordDirectoryRelativePath: "$StationId$",
                recordFileNameTemplate: "$Title$/$SYYYY$");

            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();

            var result = await service.PrepareAsync(program);
            var expectedFileName = "20260209120000_TestTitle.m4a";

            Assert.That(Path.GetFileName(result.FinalFilePath), Is.EqualTo(expectedFileName));
            Assert.That(result.RelativePath, Is.EqualTo(Path.Combine("TBS", expectedFileName)));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// 保存先相対パステンプレートが不正な場合は保存先ルートにフォールバックする
    /// </summary>
    [Test]
    public async Task PrepareAsync_不正な相対パステンプレート_保存先ルートにフォールバックする()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var configMock = CreateConfig(
                baseDir,
                tempDir,
                recordDirectoryRelativePath: "..\\Escape",
                recordFileNameTemplate: "$Title$");

            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();

            var result = await service.PrepareAsync(program);

            Assert.That(result.RelativePath, Is.EqualTo("TestTitle.m4a"));
            Assert.That(result.FinalFilePath, Is.EqualTo(Path.Combine(baseDir, "TestTitle.m4a")));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
    }

    /// <summary>
    /// ルート付きテンプレートは相対化して保存先配下として解釈する
    /// </summary>
    [Test]
    public async Task PrepareAsync_ルート付き相対パステンプレート_ルートを除去して適用する()
    {
        var baseDir = CreateTempDirectory();
        var tempDir = CreateTempDirectory();

        try
        {
            var rootedTemplate = Path.Combine(Path.GetPathRoot(baseDir)!, "Music", "$StationId$");
            var configMock = CreateConfig(
                baseDir,
                tempDir,
                recordDirectoryRelativePath: rootedTemplate,
                recordFileNameTemplate: "$Title$");

            var service = new MediaStorageService(configMock.Object);
            var program = CreateProgramInfo();

            var result = await service.PrepareAsync(program);
            var expectedRelative = Path.Combine("Music", "TBS", "TestTitle.m4a");

            Assert.That(result.RelativePath, Is.EqualTo(expectedRelative));
            Assert.That(result.FinalFilePath, Is.EqualTo(Path.Combine(baseDir, expectedRelative)));
        }
        finally
        {
            SafeDeleteDirectory(baseDir);
            SafeDeleteDirectory(tempDir);
        }
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
    /// 設定モック生成
    /// </summary>
    private static Mock<IAppConfigurationService> CreateConfig(
        string recordFileSaveDir,
        string temporaryFileSaveDir,
        string? recordDirectoryRelativePath,
        string? recordFileNameTemplate)
    {
        var mock = new Mock<IAppConfigurationService>();
        mock.SetupGet(x => x.RecordFileSaveDir).Returns(recordFileSaveDir);
        mock.SetupGet(x => x.TemporaryFileSaveDir).Returns(temporaryFileSaveDir);
        mock.SetupGet(x => x.RecordDirectoryRelativePath).Returns(recordDirectoryRelativePath);
        mock.SetupGet(x => x.RecordFileNameTemplate).Returns(recordFileNameTemplate);
        return mock;
    }

    /// <summary>
    /// 一時ディレクトリを作成
    /// </summary>
    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "MediaStorageServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 例外を握ってディレクトリ削除
    /// </summary>
    private static void SafeDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
            // テスト用のため失敗は無視
        }
    }
}
