using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Models.ExternalImport;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Logics.Logics.RecordedRadioLogic;

/// <summary>
/// 録音ファイルとDBの整合性メンテナンスを担当するロジック
/// </summary>
public class RecordingFileMaintenanceLobLogic(
    ILogger<RecordingFileMaintenanceLobLogic> logger,
    IAppConfigurationService config,
    RadioDbContext dbContext)
{
    /// <summary>
    /// 欠損ファイルレコードを抽出する
    /// </summary>
    public async ValueTask<RecordingFileMaintenanceScanResult> ScanMissingRecordsAsync(CancellationToken cancellationToken = default)
    {
        var rootPath = GetRootPath();
        var records = await LoadRecordEntriesAsync(cancellationToken);
        var fileIndex = BuildFileIndex(rootPath);

        var missingEntries = records
            .Where(entry => !File.Exists(entry.ResolvedFullPath))
            .Select(entry =>
            {
                fileIndex.TryGetValue(entry.FileName, out var candidates);
                var candidatePaths = candidates ?? [];
                return new RecordingFileMaintenanceEntry
                {
                    RecordingId = entry.RecordingId.ToString(),
                    Title = entry.Title,
                    StationName = entry.StationName,
                    StoredPath = entry.StoredPath,
                    FileName = entry.FileName,
                    CandidateCount = candidatePaths.Count,
                    CandidateRelativePaths = candidatePaths
                        .Take(5)
                        .Select(path => Path.GetRelativePath(rootPath, path))
                        .ToList()
                };
            })
            .OrderBy(x => x.StationName)
            .ThenBy(x => x.Title)
            .ToList();

        return new RecordingFileMaintenanceScanResult
        {
            MissingCount = missingEntries.Count,
            Entries = missingEntries
        };
    }

    /// <summary>
    /// 欠損ファイルレコードを同名ファイルへ再紐付けする
    /// </summary>
    public async ValueTask<RecordingFileMaintenanceActionResult> RelinkMissingRecordsAsync(
        IReadOnlyCollection<Ulid>? targetIds = null,
        CancellationToken cancellationToken = default)
    {
        var rootPath = GetRootPath();
        var records = await LoadRecordEntriesAsync(cancellationToken);
        var fileIndex = BuildFileIndex(rootPath);
        var usedPaths = records
            .Where(x => File.Exists(x.ResolvedFullPath))
            .Select(x => x.ResolvedFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetIdSet = targetIds?.ToHashSet() ?? [];

        var missingTargets = records
            .Where(entry => !File.Exists(entry.ResolvedFullPath))
            .Where(entry => targetIdSet.Count == 0 || targetIdSet.Contains(entry.RecordingId))
            .ToList();

        var result = new RecordingFileMaintenanceActionResult
        {
            TargetCount = missingTargets.Count
        };

        foreach (var target in missingTargets)
        {
            try
            {
                if (!fileIndex.TryGetValue(target.FileName, out var candidates) || candidates.Count == 0)
                {
                    result.SkipCount++;
                    result.Details.Add(CreateDetail(target.RecordingId, "skip", "同名ファイルが見つかりません。"));
                    continue;
                }

                var available = candidates
                    .Where(path => !usedPaths.Contains(path) && !reservedPaths.Contains(path))
                    .ToList();

                if (available.Count != 1)
                {
                    result.SkipCount++;
                    result.Details.Add(CreateDetail(target.RecordingId, "skip", available.Count == 0
                        ? "同名ファイルはありますが、他レコードで使用済みです。"
                        : "同名ファイルが複数見つかりました。"));
                    continue;
                }

                var newPath = available[0];
                var relativePath = Path.GetRelativePath(rootPath, newPath);

                var file = await dbContext.RecordingFiles.FindAsync([target.RecordingId], cancellationToken);
                if (file == null)
                {
                    result.FailCount++;
                    result.Details.Add(CreateDetail(target.RecordingId, "fail", "対象のファイル情報が見つかりません。"));
                    continue;
                }

                file.FileRelativePath = relativePath;
                file.HasHlsFile = false;
                file.HlsDirectoryPath = null;

                usedPaths.Add(newPath);
                reservedPaths.Add(newPath);
                result.SuccessCount++;
                result.Details.Add(CreateDetail(target.RecordingId, "success", $"再紐付けしました。({relativePath})"));
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"再紐付け処理に失敗しました。");
                result.FailCount++;
                result.Details.Add(CreateDetail(target.RecordingId, "fail", "再紐付け処理に失敗しました。"));
            }
        }

        if (result.SuccessCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// 欠損ファイルのレコードをDBから削除する
    /// </summary>
    public async ValueTask<RecordingFileMaintenanceActionResult> DeleteMissingRecordsAsync(
        IReadOnlyCollection<Ulid>? targetIds = null,
        CancellationToken cancellationToken = default)
    {
        var records = await LoadRecordEntriesAsync(cancellationToken);
        var targetIdSet = targetIds?.ToHashSet() ?? [];
        var missingTargets = records
            .Where(entry => !File.Exists(entry.ResolvedFullPath))
            .Where(entry => targetIdSet.Count == 0 || targetIdSet.Contains(entry.RecordingId))
            .ToList();

        var result = new RecordingFileMaintenanceActionResult
        {
            TargetCount = missingTargets.Count
        };

        foreach (var target in missingTargets)
        {
            try
            {
                var record = await dbContext.Recordings.FindAsync([target.RecordingId], cancellationToken);
                if (record == null)
                {
                    result.SkipCount++;
                    result.Details.Add(CreateDetail(target.RecordingId, "skip", "対象レコードは既に削除されています。"));
                    continue;
                }

                dbContext.Recordings.Remove(record);
                result.SuccessCount++;
                result.Details.Add(CreateDetail(target.RecordingId, "success", "欠損レコードを削除しました。"));
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"欠損レコード削除に失敗しました。");
                result.FailCount++;
                result.Details.Add(CreateDetail(target.RecordingId, "fail", "欠損レコード削除に失敗しました。"));
            }
        }

        if (result.SuccessCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private static RecordingFileMaintenanceActionDetail CreateDetail(Ulid recordingId, string status, string message)
    {
        return new RecordingFileMaintenanceActionDetail
        {
            RecordingId = recordingId.ToString(),
            Status = status,
            Message = message
        };
    }

    private string GetRootPath()
    {
        var root = Path.GetFullPath(config.RecordFileSaveDir);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"録音保存先が存在しません。 path={root}");
        }
        return root;
    }

    private async ValueTask<List<RecordEntry>> LoadRecordEntriesAsync(CancellationToken cancellationToken)
    {
        var rootPath = GetRootPath();
        return await dbContext.RecordingFiles
            .AsNoTracking()
            .Join(dbContext.RecordingMetadatas.AsNoTracking(),
                file => file.RecordingId,
                meta => meta.RecordingId,
                (file, meta) => new { file, meta })
            .Select(x => new RecordEntry
            {
                RecordingId = x.file.RecordingId,
                Title = x.meta.Title,
                StationName = x.meta.StationName,
                StoredPath = x.file.FileRelativePath,
                FileName = Path.GetFileName(x.file.FileRelativePath),
                ResolvedFullPath = ResolveToFullPath(rootPath, x.file.FileRelativePath)
            })
            .ToListAsync(cancellationToken);
    }

    private static Dictionary<string, List<string>> BuildFileIndex(string rootPath)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            if (!index.TryGetValue(fileName, out var values))
            {
                values = [];
                index[fileName] = values;
            }

            values.Add(path);
        }

        return index;
    }

    private static string ResolveToFullPath(string rootPath, string relativePath)
    {
        return Path.GetFullPath(Path.Combine(rootPath, relativePath));
    }

    private sealed class RecordEntry
    {
        /// <summary>
        /// 録音ID
        /// </summary>
        public Ulid RecordingId { get; set; }

        /// <summary>
        /// タイトル
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 放送局名
        /// </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary>
        /// 保存パス
        /// </summary>
        public string StoredPath { get; set; } = string.Empty;

        /// <summary>
        /// ファイル名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 絶対パス
        /// </summary>
        public string ResolvedFullPath { get; set; } = string.Empty;
    }
}
