using System.Globalization;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Infrastructure.Recording;

/// <summary>
/// 録音ファイルの保存先を解決し、移動・削除を行う実装
/// </summary>
public class MediaStorageService(IAppConfigurationService config) : IMediaStorageService
{
    /// <summary>
    /// 保存先パスを準備する
    /// </summary>
    public ValueTask<MediaPath> PrepareAsync(ProgramRecordingInfo programInfo, CancellationToken cancellationToken = default)
    {
        var fileInfo = GenerateFileName(programInfo);

        // 同一番組の同時録音でも衝突しないよう、一時ファイル名は必ずユニークにする
        var recordingsWorkDir = TemporaryStoragePaths.GetRecordingsWorkDirectory(config.TemporaryFileSaveDir);
        Directory.CreateDirectory(recordingsWorkDir);
        var tempFileName = $"{programInfo.ProgramId.ToSafeName()}_{Ulid.NewUlid()}.m4a";
        var tempFilePath = Path.Combine(recordingsWorkDir, tempFileName);

        return ValueTask.FromResult(new MediaPath(
            TempFilePath: tempFilePath,
            FinalFilePath: fileInfo.FileFullPath,
            RelativePath: fileInfo.FileRelativePath));
    }

    /// <summary>
    /// 一時ファイルを最終保存先へ確定させる
    /// </summary>
    public ValueTask<MediaPath> CommitAsync(MediaPath path, CancellationToken cancellationToken = default)
    {
        // 保存先ディレクトリが無ければ作成
        var dir = Path.GetDirectoryName(path.FinalFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var finalFilePath = path.FinalFilePath;
        var relativePath = path.RelativePath;

        // 同名ファイルがある場合はリネームして保存
        if (File.Exists(path.FinalFilePath))
        {
            finalFilePath = AddDuplicateSuffix(path.FinalFilePath);
            File.Move(path.TempFilePath, finalFilePath);

            var relativeDirectory = Path.GetDirectoryName(path.RelativePath) ?? string.Empty;
            var renamedFileName = Path.GetFileName(finalFilePath);
            relativePath = string.IsNullOrEmpty(relativeDirectory)
                ? renamedFileName
                : Path.Combine(relativeDirectory, renamedFileName);
        }
        else
        {
            File.Move(path.TempFilePath, path.FinalFilePath);
        }

        return ValueTask.FromResult(path with
        {
            FinalFilePath = finalFilePath,
            RelativePath = relativePath
        });
    }

    /// <summary>
    /// 一時ファイルを削除する
    /// </summary>
    public ValueTask CleanupTempAsync(MediaPath path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path.TempFilePath))
        {
            File.Delete(path.TempFilePath);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// ファイル名をテンプレートから生成する
    /// </summary>
    private RadioFileInfo GenerateFileName(ProgramRecordingInfo programInfo)
    {
        var (fullPath, relativePath) = GenerateSavePath(programInfo);

        var template = config.RecordFileNameTemplate;
        if (!string.IsNullOrEmpty(template))
        {
            var replacements = ReplaceTemplate(programInfo);

            foreach (var placeholder in replacements)
            {
                template = template.Replace(placeholder.Key, placeholder.Value);
            }

            if (template.IsValidFileName())
            {
                return new RadioFileInfo(template, fullPath, relativePath);
            }
        }

        return new RadioFileInfo($"{programInfo.StartTime.ToJapanDateTime():yyyyMMddHHmmss}_{programInfo.Title.ToSafeName()}", fullPath, relativePath);
    }

    /// <summary>
    /// 保存先ディレクトリをテンプレートから生成する
    /// </summary>
    private (string FullPath, string RelativePath) GenerateSavePath(ProgramRecordingInfo programInfo)
    {
        var template = config.RecordDirectoryRelativePath;

        if (!string.IsNullOrEmpty(template))
        {
            template = NormalizePathSeparators(template);

            var replacements = ReplaceTemplate(programInfo);

            foreach (var placeholder in replacements)
            {
                template = template.Replace(placeholder.Key, placeholder.Value);
            }

            if (Path.IsPathRooted(template))
            {
                template = template.TrimStart(Path.GetPathRoot(template)?.ToCharArray());
            }

            if (!config.RecordFileSaveDir.IsValidAbsolutePath())
            {
                throw new DomainException("録音フォルダの設定が不正です。");
            }

            if (!template.IsValidRelativePath())
            {
                return (config.RecordFileSaveDir, string.Empty);
            }

            if (config.RecordFileSaveDir.TryCombinePaths(template, out var path))
            {
                return (path, template);
            }
        }

        return (config.RecordFileSaveDir, string.Empty);
    }

    private static string NormalizePathSeparators(string path)
    {
        return path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// テンプレート置換用の辞書を生成する
    /// </summary>
    private Dictionary<string, string> ReplaceTemplate(ProgramRecordingInfo programInfo) => new()
    {
        { "$StationId$", programInfo.StationId.ToSafeName() },
        { "$StationName$", programInfo.StationName.ToSafeName() },
        { "$Title$", programInfo.Title.ToSafeName() },
        { "$SYYYY$", programInfo.StartTime.ToJapanDateTime().ToString("yyyy", CultureInfo.InvariantCulture) },
        { "$SYY$", programInfo.StartTime.ToJapanDateTime().ToString("yy", CultureInfo.InvariantCulture) },
        { "$SMM$", programInfo.StartTime.ToJapanDateTime().ToString("MM", CultureInfo.InvariantCulture) },
        { "$SM$", programInfo.StartTime.ToJapanDateTime().Month.ToString() },
        { "$SDD$", programInfo.StartTime.ToJapanDateTime().ToString("dd", CultureInfo.InvariantCulture) },
        { "$SD$", programInfo.StartTime.ToJapanDateTime().Day.ToString() },
        { "$STHH$", programInfo.StartTime.ToJapanDateTime().ToString("HH", CultureInfo.InvariantCulture) },
        { "$STH$", programInfo.StartTime.ToJapanDateTime().Hour.ToString() },
        { "$STMM$", programInfo.StartTime.ToJapanDateTime().ToString("mm", CultureInfo.InvariantCulture) },
        { "$STM$", programInfo.StartTime.ToJapanDateTime().Minute.ToString() },
        { "$STSS$", programInfo.StartTime.ToJapanDateTime().ToString("ss", CultureInfo.InvariantCulture) },
        { "$STS$", programInfo.StartTime.ToJapanDateTime().Second.ToString() },
        { "$EYYYY$", programInfo.EndTime.ToJapanDateTime().ToString("yyyy", CultureInfo.InvariantCulture) },
        { "$EYY$", programInfo.EndTime.ToJapanDateTime().ToString("yy", CultureInfo.InvariantCulture) },
        { "$EMM$", programInfo.EndTime.ToJapanDateTime().ToString("MM", CultureInfo.InvariantCulture) },
        { "$EM$", programInfo.EndTime.ToJapanDateTime().Month.ToString() },
        { "$EDD$", programInfo.EndTime.ToJapanDateTime().ToString("dd", CultureInfo.InvariantCulture) },
        { "$ED$", programInfo.EndTime.ToJapanDateTime().Day.ToString() },
        { "$ETHH$", programInfo.EndTime.ToJapanDateTime().ToString("HH", CultureInfo.InvariantCulture) },
        { "$ETH$", programInfo.EndTime.ToJapanDateTime().Hour.ToString() },
        { "$ETMM$", programInfo.EndTime.ToJapanDateTime().ToString("mm", CultureInfo.InvariantCulture) },
        { "$ETM$", programInfo.EndTime.ToJapanDateTime().Minute.ToString() },
        { "$ETSS$", programInfo.EndTime.ToJapanDateTime().ToString("ss", CultureInfo.InvariantCulture) },
        { "$ETS$", programInfo.EndTime.ToJapanDateTime().Second.ToString() },
    };

    /// <summary>
    /// 重複ファイル名にサフィックスを付与する
    /// </summary>
    private static string AddDuplicateSuffix(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);

        var suffix = $"_duplicate_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Ulid.NewUlid()}";
        return Path.Combine(dir, name + suffix + ext);
    }
}
