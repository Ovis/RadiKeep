using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Logics.NotificationLogic;
using RadiKeep.Logics.Logics.RecordedRadioLogic;
using RadiKeep.Logics.Models.ExternalImport;
using ZLogger;

namespace RadiKeep.Areas.Api.Controllers;

[Area("api")]
[ApiController]
[Route("/api/v1/settings/external-import")]
public class ExternalImportApiController(
    ILogger<ExternalImportApiController> logger,
    ExternalRecordingImportLobLogic importLobLogic,
    RecordingFileMaintenanceLobLogic recordingFileMaintenanceLobLogic,
    NotificationLobLogic notificationLobLogic) : ControllerBase
{
    private const long CsvMaxFileSize = 5 * 1024 * 1024;

    /// <summary>
    /// 録音保存先をスキャンして未登録候補を取得
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("scan")]
    public async ValueTask<IActionResult> ScanAsync([FromBody] ExternalImportScanRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var applyDefaultTag = request?.ApplyDefaultTag ?? true;
            var candidates = await importLobLogic.ScanCandidatesAsync(applyDefaultTag, cancellationToken);
            return Ok(ApiResponse.Ok(candidates, "スキャンが完了しました。"));
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"外部取込スキャンでエラーが発生しました。");
            return BadRequest(ApiResponse.Fail("スキャンに失敗しました。"));
        }
    }

    /// <summary>
    /// 候補一覧をCSVでダウンロード
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("export-csv")]
    public IActionResult ExportCsv([FromBody] ExternalImportCandidatesRequest request)
    {
        var bytes = importLobLogic.ExportCandidatesCsv(request.Candidates);
        var fileName = $"external-import-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// CSVをアップロードして候補一覧を再構築
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("import-csv")]
    public async ValueTask<IActionResult> ImportCsv([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("CSVファイルが指定されていません。"));
        }

        if (file.Length > CsvMaxFileSize)
        {
            return BadRequest(ApiResponse.Fail("CSVファイルサイズが上限を超えています。"));
        }

        await using var stream = file.OpenReadStream();
        var (isSuccess, candidates, errors) = await importLobLogic.ImportCandidatesCsvAsync(stream, cancellationToken);
        if (!isSuccess)
        {
            return BadRequest(ApiResponse.Fail(string.Join("\n", errors)));
        }

        return Ok(ApiResponse.Ok(candidates, "CSVを反映しました。"));
    }

    /// <summary>
    /// 候補を録音済み番組へ保存
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("save")]
    public async ValueTask<IActionResult> SaveAsync([FromBody] ExternalImportCandidatesRequest request, CancellationToken cancellationToken)
    {
        var result = await importLobLogic.SaveCandidatesAsync(request.Candidates, request.MarkAsListened, cancellationToken);
        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                await notificationLobLogic.SetNotificationAsync(
                    LogLevel.Error,
                    NoticeCategory.RecordingError,
                    string.IsNullOrWhiteSpace(error.FilePath)
                        ? error.Message
                        : $"外部取込エラー: {Path.GetFileName(error.FilePath)} - {error.Message}");
            }

            var response = new ApiResponse<ExternalImportSaveResult>(
                false,
                result,
                new ApiError("validation_error", "入力内容にエラーがあります。"),
                "入力内容にエラーがあります。");
            return BadRequest(response);
        }

        await notificationLobLogic.SetNotificationAsync(
            LogLevel.Information,
            NoticeCategory.RecordingSuccess,
            $"外部音声ファイルを {result.SavedCount} 件取り込みました。");

        return Ok(ApiResponse.Ok(result, "取り込みが完了しました。"));
    }

    /// <summary>
    /// 録音ファイル欠損レコードをスキャンする
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("maintenance/scan-missing")]
    public async ValueTask<IActionResult> ScanMissingRecordsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await recordingFileMaintenanceLobLogic.ScanMissingRecordsAsync(cancellationToken);
            return Ok(ApiResponse.Ok(result, "欠損レコードのスキャンが完了しました。"));
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"欠損レコードスキャンでエラーが発生しました。");
            return BadRequest(ApiResponse.Fail("欠損レコードのスキャンに失敗しました。"));
        }
    }

    /// <summary>
    /// 欠損レコードのファイル再紐付けを実行する
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("maintenance/relink-missing")]
    public async ValueTask<IActionResult> RelinkMissingRecordsAsync([FromBody] RecordingMaintenanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var ids = request.RecordingIds
                .Select(id => Ulid.TryParse(id, out var value) ? value : (Ulid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            var result = await recordingFileMaintenanceLobLogic.RelinkMissingRecordsAsync(ids, cancellationToken);
            return Ok(ApiResponse.Ok(result, "再紐付け処理が完了しました。"));
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"欠損レコード再紐付けでエラーが発生しました。");
            return BadRequest(ApiResponse.Fail("再紐付け処理に失敗しました。"));
        }
    }

    /// <summary>
    /// 欠損レコードをDBから削除する
    /// </summary>
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    [Route("maintenance/delete-missing")]
    public async ValueTask<IActionResult> DeleteMissingRecordsAsync([FromBody] RecordingMaintenanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var ids = request.RecordingIds
                .Select(id => Ulid.TryParse(id, out var value) ? value : (Ulid?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            var result = await recordingFileMaintenanceLobLogic.DeleteMissingRecordsAsync(ids, cancellationToken);
            return Ok(ApiResponse.Ok(result, "欠損レコード削除が完了しました。"));
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"欠損レコード削除でエラーが発生しました。");
            return BadRequest(ApiResponse.Fail("欠損レコード削除に失敗しました。"));
        }
    }
}

public class ExternalImportCandidatesRequest
{
    public List<ExternalImportCandidateEntry> Candidates { get; set; } = [];
    public bool MarkAsListened { get; set; } = false;
}

public class ExternalImportScanRequest
{
    public bool ApplyDefaultTag { get; set; } = true;
}

public class RecordingMaintenanceRequest
{
    public List<string> RecordingIds { get; set; } = [];
}
