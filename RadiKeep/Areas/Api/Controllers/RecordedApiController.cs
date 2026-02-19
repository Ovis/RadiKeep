using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Infrastructure.Recording;
using RadiKeep.Logics.Logics.RecordedRadioLogic;
using RadiKeep.Logics.Logics.TagLogic;
using RadiKeep.Logics.Services;

namespace RadiKeep.Areas.Api.Controllers
{
    [Area("api")]
    [ApiController]
    [AutoValidateAntiforgeryToken]
    [Route("/api/v1/recordings")]
    public class RecordedApiController(
        IAppConfigurationService config,
        RecordedRadioLobLogic recordedRadioLobLogic,
        RecordedDuplicateDetectionLobLogic duplicateDetectionLobLogic,
        TagLobLogic tagLobLogic) : ControllerBase
    {
        [HttpGet]
        [Route("")]
        public async ValueTask<IActionResult> GetRecordedRadio(
            int page = 1,
            int pageSize = 10,
            string searchQuery = "",
            string sortBy = "StartDateTime",
            bool isDescending = true,
            int? withinDays = null,
            string stationId = "",
            string tagIds = "",
            string tagMode = "or",
            bool untaggedOnly = false,
            bool unlistenedOnly = false)
        {
            var parsedTagIds = tagIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var id) ? id : (Guid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            var (isSuccess, total, list, _) = await recordedRadioLobLogic.GetRecorderProgramListAsync(
                searchQuery,
                page,
                pageSize,
                sortBy,
                isDescending,
                withinDays,
                stationId,
                parsedTagIds,
                tagMode,
                untaggedOnly,
                unlistenedOnly);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail("録音済み番組の取得に失敗しました。"));
            }

            var response = new
            {
                TotalRecords = total,
                Page = page,
                PageSize = pageSize,
                Recordings = list
            };

            return Ok(ApiResponse.Ok(response));
        }

        [HttpGet]
        [Route("stations")]
        public async ValueTask<IActionResult> GetRecordedStations()
        {
            var (isSuccess, list, _) = await recordedRadioLobLogic.GetRecordedStationFiltersAsync();
            if (!isSuccess || list == null)
            {
                return BadRequest(ApiResponse.Fail("放送局フィルタ候補の取得に失敗しました。"));
            }

            return Ok(ApiResponse.Ok(list));
        }

        /// <summary>
        /// 類似録音抽出ジョブを即時実行する
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("duplicates/run")]
        public async ValueTask<IActionResult> RunDuplicateDetectionAsync(DuplicateDetectionRunRequest? request)
        {
            var lookbackDays = request?.LookbackDays ?? 30;
            var maxPhase1Groups = request?.MaxPhase1Groups ?? 100;
            var phase2Mode = request?.Phase2Mode ?? "light";
            var broadcastClusterWindowHours = request?.BroadcastClusterWindowHours ?? 48;
            var (isSuccess, message, _) = await duplicateDetectionLobLogic.StartImmediateAsync(
                lookbackDays,
                maxPhase1Groups,
                phase2Mode,
                broadcastClusterWindowHours);
            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(message));
            }

            return Ok(ApiResponse.Ok(message));
        }

        /// <summary>
        /// 類似録音抽出ジョブの実行状態を取得する
        /// </summary>
        [HttpGet]
        [Route("duplicates/status")]
        public IActionResult GetDuplicateDetectionStatus()
        {
            return Ok(ApiResponse.Ok(duplicateDetectionLobLogic.GetStatus()));
        }

        /// <summary>
        /// 直近の類似録音抽出結果を取得する
        /// </summary>
        [HttpGet]
        [Route("duplicates/candidates")]
        public IActionResult GetDuplicateDetectionCandidates()
        {
            return Ok(ApiResponse.Ok(duplicateDetectionLobLogic.GetLastCandidates()));
        }


        [HttpGet]
        [Route("download/{recordId}")]
        public async ValueTask<IActionResult> DownloadFile(string recordId)
        {
            if (string.IsNullOrEmpty(recordId))
            {
                return BadRequest("recordId is required.");
            }

            if (!Ulid.TryParse(recordId, out var recordUlid))
            {
                return BadRequest("Invalid record id.");
            }

            string filePath;
            {
                (var isSuccess, filePath) = await recordedRadioLobLogic.GetRecordedProgramFilePathAsync(recordUlid);

                if (!isSuccess)
                {
                    return NotFound("File not found.");
                }
            }

            await recordedRadioLobLogic.MarkAsListenedAsync(recordUlid);

            if (!TryResolveManagedPath(filePath, out var fileFullPath, config.RecordFileSaveDir))
            {
                return BadRequest("Invalid file path.");
            }

            if (!System.IO.File.Exists(fileFullPath))
            {
                return NotFound("File not found.");
            }

            // ファイル名の設定
            var fileName = Path.GetFileName(fileFullPath);

            var fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read);

            return File(fileStream, "application/octet-stream", fileName);
        }


        [HttpGet]
        [Route("play/{recordId}")]
        public async ValueTask<IActionResult> Play(string recordId)
        {
            if (string.IsNullOrEmpty(recordId))
            {
                return BadRequest("recordId is required.");
            }

            if (!Ulid.TryParse(recordId, out var recordUlid))
            {
                return BadRequest("Invalid record id.");
            }

            string filePath;
            {
                (var isSuccess, filePath) = await recordedRadioLobLogic.GetHlsAsync(recordUlid);

                if (!isSuccess)
                {
                    return NotFound("File not found.");
                }
            }

            await recordedRadioLobLogic.MarkAsListenedAsync(recordUlid);

            var hlsRoot = TemporaryStoragePaths.GetHlsCacheRootDirectory(config.TemporaryFileSaveDir);
            if (!TryResolveManagedPath(filePath, out var fileFullPath, config.RecordFileSaveDir, hlsRoot))
            {
                return BadRequest("Invalid file path.");
            }

            if (!System.IO.File.Exists(fileFullPath))
            {
                return NotFound("File not found.");
            }

            var m3u8Content = await System.IO.File.ReadAllTextAsync(fileFullPath);

            // m3u8Contentを返す
            return Content(m3u8Content, "application/x-mpegURL");
        }



        [HttpPost]
        [Route("delete")]
        public async ValueTask<IActionResult> DeleteProgram(ReserveEntryRequest delEntry)
        {
            if (string.IsNullOrEmpty(delEntry.Id))
            {
                return BadRequest(ApiResponse.Fail("recordId is required."));
            }

            if (!Ulid.TryParse(delEntry.Id, out var recordUlid))
            {
                return BadRequest(ApiResponse.Fail("Invalid record id."));
            }

            var record = await recordedRadioLobLogic.CheckProgramExistsAsync(recordUlid);

            if (!record.IsSuccess)
            {
                return BadRequest(ApiResponse.Fail("recordId is required."));
            }

            if (!record.IsExists)
            {
                return BadRequest(ApiResponse.Fail("指定された番組はすでに削除されています。"));
            }

            var result = await recordedRadioLobLogic.DeleteRecordedProgramAsync(recordUlid, true);


            return Ok(ApiResponse.Ok(result, result ? "削除しました。" : "削除に失敗しました。"));
        }

        [HttpPost]
        [Route("delete/bulk")]
        public async ValueTask<IActionResult> BulkDeletePrograms(RecordingBulkDeleteRequest request)
        {
            if (request.RecordingIds == null || request.RecordingIds.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("削除対象が選択されていません。"));
            }

            var recordingIds = request.RecordingIds
                .Select(id => Ulid.TryParse(id, out var value) ? value : (Ulid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (recordingIds.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("有効な録音IDが含まれていません。"));
            }

            var successCount = 0;
            var skipCount = 0;
            var failCount = 0;
            var failedRecordingIds = new List<string>();
            var skippedRecordingIds = new List<string>();

            foreach (var id in recordingIds)
            {
                var exists = await recordedRadioLobLogic.CheckProgramExistsAsync(id);
                if (!exists.IsSuccess)
                {
                    failCount++;
                    failedRecordingIds.Add(id.ToString());
                    continue;
                }

                if (!exists.IsExists)
                {
                    skipCount++;
                    skippedRecordingIds.Add(id.ToString());
                    continue;
                }

                var deleted = await recordedRadioLobLogic.DeleteRecordedProgramAsync(id, request.DeleteFiles);
                if (deleted)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    failedRecordingIds.Add(id.ToString());
                }
            }

            var result = new
            {
                SuccessCount = successCount,
                SkipCount = skipCount,
                FailCount = failCount,
                FailedRecordingIds = failedRecordingIds,
                SkippedRecordingIds = skippedRecordingIds
            };

            if (successCount == 0)
            {
                if (skipCount > 0 && failCount == 0)
                {
                    return Ok(ApiResponse.Ok(result, "削除対象はすでに削除されています。"));
                }

                return BadRequest(ApiResponse.Fail("削除に失敗しました。"));
            }

            var message = (skipCount, failCount) switch
            {
                (0, 0) => $"{successCount}件を削除しました。",
                _ => $"{successCount}件削除、{skipCount}件スキップ、{failCount}件失敗しました。"
            };

            return Ok(ApiResponse.Ok(result, message));
        }

        [HttpPost]
        [Route("{id}/tags")]
        public async ValueTask<IActionResult> AddTagsToRecording(string id, RecordingTagsRequest request)
        {
            if (!Ulid.TryParse(id, out var recordingId))
            {
                return BadRequest(ApiResponse.Fail("Invalid record id."));
            }

            try
            {
                await tagLobLogic.AddTagsToRecordingAsync(recordingId, request.TagIds);
                return Ok(ApiResponse.Ok("更新しました。"));
            }
            catch (DomainException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.UserMessage));
            }
        }

        [HttpDelete]
        [Route("{id}/tags/{tagId}")]
        public async ValueTask<IActionResult> RemoveTagFromRecording(string id, Guid tagId)
        {
            if (!Ulid.TryParse(id, out var recordingId))
            {
                return BadRequest(ApiResponse.Fail("Invalid record id."));
            }

            await tagLobLogic.RemoveTagFromRecordingAsync(recordingId, tagId);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        [HttpPost]
        [Route("tags/bulk-add")]
        public async ValueTask<IActionResult> BulkAddTagsToRecordings(RecordingBulkTagRequest request)
        {
            var recordingIds = request.RecordingIds
                .Select(id => Ulid.TryParse(id, out var value) ? value : (Ulid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            try
            {
                var result = await tagLobLogic.BulkAddTagsToRecordingsAsync(recordingIds, request.TagIds);
                return Ok(ApiResponse.Ok(result));
            }
            catch (DomainException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.UserMessage));
            }
        }

        [HttpPost]
        [Route("tags/bulk-remove")]
        public async ValueTask<IActionResult> BulkRemoveTagsFromRecordings(RecordingBulkTagRequest request)
        {
            var recordingIds = request.RecordingIds
                .Select(id => Ulid.TryParse(id, out var value) ? value : (Ulid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            try
            {
                var result = await tagLobLogic.BulkRemoveTagsFromRecordingsAsync(recordingIds, request.TagIds);
                return Ok(ApiResponse.Ok(result));
            }
            catch (DomainException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.UserMessage));
            }
        }

        [HttpPost]
        [Route("listened/bulk")]
        public async ValueTask<IActionResult> BulkUpdateListenedState(RecordingBulkListenedRequest request)
        {
            if (request.RecordingIds == null || request.RecordingIds.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("更新対象が選択されていません。"));
            }

            var recordingIds = request.RecordingIds
                .Select(id => Ulid.TryParse(id, out var value) ? value : (Ulid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (recordingIds.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("有効な録音IDが含まれていません。"));
            }

            var result = await recordedRadioLobLogic.BulkUpdateListenedStateAsync(recordingIds, request.IsListened);
            var responseData = new
            {
                result.SuccessCount,
                result.SkipCount,
                result.FailCount,
                result.FailedRecordingIds,
                result.SkippedRecordingIds
            };
            var message = request.IsListened
                ? $"{result.SuccessCount}件を視聴済みに更新しました。"
                : $"{result.SuccessCount}件を未視聴に更新しました。";

            if (result.SkipCount > 0 || result.FailCount > 0)
            {
                message = $"{message} スキップ:{result.SkipCount}件 / 失敗:{result.FailCount}件";
            }

            return Ok(ApiResponse.Ok(responseData, message));
        }

        private static bool TryResolveManagedPath(string storedPath, out string fullPath, params string[] allowedBaseDirs)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(storedPath) || allowedBaseDirs.Length == 0)
            {
                return false;
            }

            var normalizedBases = allowedBaseDirs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Path.GetFullPath(x).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .ToList();

            if (normalizedBases.Count == 0)
            {
                return false;
            }

            if (Path.IsPathRooted(storedPath))
            {
                var candidate = Path.GetFullPath(storedPath);
                if (!IsUnderAnyBase(candidate, normalizedBases))
                {
                    return false;
                }

                fullPath = candidate;
                return true;
            }

            foreach (var baseDir in normalizedBases)
            {
                if (baseDir.TryCombinePaths(storedPath, out var combined))
                {
                    fullPath = combined;
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnderAnyBase(string candidatePath, IReadOnlyList<string> baseDirs)
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            foreach (var baseDir in baseDirs)
            {
                var withSeparator = baseDir + Path.DirectorySeparatorChar;
                if (candidatePath.Equals(baseDir, comparison) || candidatePath.StartsWith(withSeparator, comparison))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
