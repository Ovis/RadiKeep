using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace RadiKeep.Endpoints;

public static class VnextEndpoints
{
    public static IEndpointRouteBuilder MapVnextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vnext").WithTags("VNext");
        group.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        return endpoints;
    }
}
