using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Logics.RecordedRadioLogic;
using RadiKeep.Logics.Models;

namespace RadiKeep.Features.Recording;

public static class ListRecordings
{
    public static IEndpointRouteBuilder MapRecordingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vnext/recordings").WithTags("RecordingsVNext");
        group.MapGet("/", HandleAsync)
            .WithName("VNextListRecordings")
            .WithSummary("録音済み一覧を取得する");
        return endpoints;
    }

    private static async Task<Results<Ok<ApiResponse<ListRecordingsResponse>>, BadRequest<ApiResponse<object?>>>> HandleAsync(
        [AsParameters] ListRecordingsQuery query,
        [FromServices] RecordedRadioLobLogic recordedRadioLobLogic)
    {
        var parsedTagIds = (query.TagIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var id) ? id : (Guid?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        var (isSuccess, total, list, _) = await recordedRadioLobLogic.GetRecorderProgramListAsync(
            query.SearchQuery ?? string.Empty,
            query.Page,
            query.PageSize,
            query.SortBy ?? "StartDateTime",
            query.IsDescending,
            query.WithinDays,
            query.StationId ?? string.Empty,
            parsedTagIds,
            query.TagMode ?? "or",
            query.UntaggedOnly,
            query.UnlistenedOnly);

        if (!isSuccess || list == null)
        {
            return TypedResults.BadRequest(ApiResponse.Fail("録音済み番組の取得に失敗しました。"));
        }

        var response = new ListRecordingsResponse(
            total,
            query.Page,
            query.PageSize,
            list);

        return TypedResults.Ok(ApiResponse.Ok(response));
    }
}

public sealed record ListRecordingsQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchQuery { get; init; }
    public string? SortBy { get; init; } = "StartDateTime";
    public bool IsDescending { get; init; } = true;
    public int? WithinDays { get; init; }
    public string? StationId { get; init; }
    public string? TagIds { get; init; }
    public string? TagMode { get; init; } = "or";
    public bool UntaggedOnly { get; init; }
    public bool UnlistenedOnly { get; init; }
}

public sealed record ListRecordingsResponse(
    int TotalRecords,
    int Page,
    int PageSize,
    List<RecordedProgramEntry> Recordings);
