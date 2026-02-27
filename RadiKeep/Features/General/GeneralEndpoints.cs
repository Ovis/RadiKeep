using Microsoft.AspNetCore.Http.HttpResults;
using RadiKeep.Features.Shared.Models;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Extensions;

namespace RadiKeep.Features.General;

/// <summary>
/// 共通情報取得の Api エンドポイントを提供する。
/// </summary>
public static class GeneralEndpoints
{
    /// <summary>
    /// 共通情報取得エンドポイントをマッピングする。
    /// </summary>
    public static IEndpointRouteBuilder MapGeneralEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/general").WithTags("GeneralApi");
        group.MapGet("/radio-dates", HandleGetRadioDates)
            .WithName("ApiGeneralRadioDates")
            .WithSummary("番組表で選択可能な日付を取得する");
        return endpoints;
    }

    /// <summary>
    /// 番組表で選択可能な日付を取得する。
    /// </summary>
    private static Ok<ApiResponse<List<object>>> HandleGetRadioDates(IRadioAppContext context)
    {
        var today = context.StandardDateTimeOffset.ToRadioDate();
        var dates = Enumerable.Range(-7, 14)
            .Select(i =>
            {
                var date = today.AddDays(i);
                return (object)new
                {
                    Value = date.ToString("yyyyMMdd"),
                    TextContent = date.ToString("yyyy/MM/dd(ddd)", context.CultureInfo),
                    IsToday = date == today
                };
            })
            .ToList();

        return TypedResults.Ok(ApiResponse.Ok(dates));
    }
}


