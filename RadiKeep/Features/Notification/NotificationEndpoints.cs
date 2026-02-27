using Microsoft.AspNetCore.Http.HttpResults;
using RadiKeep.Features.Shared.Models;
using RadiKeep.Logics.Logics.NotificationLogic;

namespace RadiKeep.Features.Notification;

/// <summary>
/// お知らせ関連の Api エンドポイントを提供する。
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>
    /// お知らせ関連エンドポイントをマッピングする。
    /// </summary>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").WithTags("NotificationsApi");
        group.MapGet("/count", HandleUnreadCountAsync)
            .WithName("ApiNotificationCount")
            .WithSummary("未読のお知らせ件数を取得する");
        group.MapGet("/latest", HandleUnreadLatestAsync)
            .WithName("ApiNotificationLatest")
            .WithSummary("未読のお知らせ一覧を取得する");
        group.MapGet("/", HandleListAsync)
            .WithName("ApiNotificationList")
            .WithSummary("お知らせ一覧を取得する");
        group.MapPost("/clear", HandleClearAsync)
            .WithName("ApiNotificationClear")
            .WithSummary("お知らせを全削除する");
        return endpoints;
    }

    /// <summary>
    /// 未読のお知らせ件数を取得する。
    /// </summary>
    private static async Task<Ok<ApiResponse<int>>> HandleUnreadCountAsync(
        NotificationLobLogic notificationLobLogic)
    {
        var count = await notificationLobLogic.GetUnreadNotificationCountAsync();
        return TypedResults.Ok(ApiResponse.Ok(count));
    }

    /// <summary>
    /// 未読のお知らせ一覧を取得する。
    /// </summary>
    private static async Task<Ok<ApiResponse<object>>> HandleUnreadLatestAsync(
        NotificationLobLogic notificationLobLogic)
    {
        var list = await notificationLobLogic.GetUnreadNotificationListAsync();
        var data = new
        {
            Count = list.Count,
            List = list.Take(5)
        };
        return TypedResults.Ok(ApiResponse.Ok((object)data));
    }

    /// <summary>
    /// お知らせ一覧を取得する。
    /// </summary>
    private static async Task<Results<Ok<ApiResponse<object>>, BadRequest<ApiResponse<object?>>>> HandleListAsync(
        NotificationLobLogic notificationLobLogic,
        int page = 1,
        int pageSize = 20)
    {
        var (isSuccess, total, list, _) = await notificationLobLogic.GetNotificationListAsync(page, pageSize);
        if (!isSuccess)
        {
            return TypedResults.BadRequest(ApiResponse.Fail("お知らせの取得に失敗しました。"));
        }

        var data = new
        {
            TotalRecords = total,
            Page = page,
            PageSize = pageSize,
            Recordings = list
        };

        return TypedResults.Ok(ApiResponse.Ok((object)data));
    }

    /// <summary>
    /// お知らせを全削除する。
    /// </summary>
    private static async Task<Ok<ApiResponse<object?>>> HandleClearAsync(
        NotificationLobLogic notificationLobLogic)
    {
        await notificationLobLogic.DeleteAllNotificationAsync();
        return TypedResults.Ok(ApiResponse.Ok("削除しました。"));
    }
}


