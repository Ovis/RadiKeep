using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.TagLogic;

namespace RadiKeep.Areas.Api.Controllers;

[Area("api")]
[ApiController]
[Route("/api/v1/tags")]
public class TagApiController(TagLobLogic tagLobLogic) : ControllerBase
{
    [HttpGet]
    [Route("")]
    public async ValueTask<IActionResult> GetTags([FromQuery] string keyword = "")
    {
        var list = await tagLobLogic.GetTagsAsync(keyword);
        return Ok(ApiResponse.Ok(list));
    }

    [HttpPost]
    [Route("")]
    public async ValueTask<IActionResult> CreateTag(TagUpsertRequest request)
    {
        try
        {
            var created = await tagLobLogic.CreateTagAsync(request.Name);
            return Ok(ApiResponse.Ok(created, "作成しました。"));
        }
        catch (DomainException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.UserMessage));
        }
    }

    [HttpPatch]
    [Route("{id:guid}")]
    public async ValueTask<IActionResult> UpdateTag(Guid id, TagUpsertRequest request)
    {
        try
        {
            var updated = await tagLobLogic.UpdateTagAsync(id, request.Name);
            return Ok(ApiResponse.Ok(updated, "更新しました。"));
        }
        catch (DomainException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.UserMessage));
        }
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public async ValueTask<IActionResult> DeleteTag(Guid id)
    {
        await tagLobLogic.DeleteTagAsync(id);
        return Ok(ApiResponse.Ok("削除しました。"));
    }

    [HttpPost]
    [Route("merge")]
    public async ValueTask<IActionResult> MergeTag(TagMergeRequest request)
    {
        try
        {
            await tagLobLogic.MergeTagAsync(request.FromTagId, request.ToTagId);
            return Ok(ApiResponse.Ok("統合しました。"));
        }
        catch (DomainException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.UserMessage));
        }
    }
}
