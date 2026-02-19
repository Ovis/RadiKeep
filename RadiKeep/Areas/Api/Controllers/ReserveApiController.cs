using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Logics.ReserveLogic;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;

namespace RadiKeep.Areas.Api.Controllers
{
    [Area("api")]
    [ApiController]
    [Route("/api/v1/reserves")]
    public class ReserveApiController(
        ReserveLobLogic reserveLobLogic) : ControllerBase
    {
        [HttpGet]
        [Route("keywords")]
        public async ValueTask<IActionResult> GetKeywordReserveList()
        {

            var (isSuccess, entry, error) = await reserveLobLogic.GetKeywordReserveListAsync();

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok(entry));
        }

        [HttpPost]
        [Route("keywords/update")]
        public async ValueTask<IActionResult> UpdateKeywordReserveEntry(KeywordReserveEntry entry)
        {
            if (!Enum.IsDefined(typeof(KeywordReserveTagMergeBehavior), entry.MergeTagBehavior))
            {
                return BadRequest(ApiResponse.Fail("タグマージ設定が不正です。"));
            }

            var (isSuccess, error) = await reserveLobLogic.UpdateKeywordReserveAsync(entry);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        [HttpPost]
        [Route("keywords/delete")]
        public async ValueTask<IActionResult> DeleteKeywordReserveEntry(ReserveEntryRequest request)
        {
            if (!TryParseReserveId(request.Id, out var reserveId))
            {
                return BadRequest(ApiResponse.Fail("Invalid reserve id."));
            }

            var (isSuccess, error) = await reserveLobLogic.DeleteKeywordReserveAsync(reserveId);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok("削除しました。"));
        }


        [HttpGet]
        [Route("programs")]
        public async ValueTask<IActionResult> GetReserveList()
        {

            var (isSuccess, entry, error) = await reserveLobLogic.GetReserveListAsync();

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok(entry));
        }


        [HttpPost]
        [Route("programs/delete")]
        public async ValueTask<IActionResult> DeleteProgramReserveEntry(ReserveEntryRequest request)
        {
            if (!TryParseReserveId(request.Id, out var reserveId))
            {
                return BadRequest(ApiResponse.Fail("Invalid reserve id."));
            }

            var (isSuccess, error) = await reserveLobLogic.DeleteProgramReserveEntryAsync(reserveId);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok("削除しました。"));
        }

        [HttpPost]
        [Route("keywords/switch")]
        public async ValueTask<IActionResult> SwitchKeywordReserveReserveEntryStatus(ReserveEntryRequest request)
        {
            if (!TryParseReserveId(request.Id, out var reserveId))
            {
                return BadRequest(ApiResponse.Fail("Invalid reserve id."));
            }

            var (isSuccess, error) = await reserveLobLogic.SwitchKeywordReserveEntryStatusAsync(reserveId);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        [HttpPost]
        [Route("keywords/reorder")]
        public async ValueTask<IActionResult> ReorderKeywordReserves(KeywordReserveReorderRequest request)
        {
            if (request.Ids.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("並び替え対象が指定されていません。"));
            }

            var orderedIds = new List<Ulid>(request.Ids.Count);
            foreach (var id in request.Ids)
            {
                if (!TryParseReserveId(id, out var reserveId))
                {
                    return BadRequest(ApiResponse.Fail("Invalid reserve id."));
                }

                orderedIds.Add(reserveId);
            }

            var (isSuccess, error) = await reserveLobLogic.ReorderKeywordReservesAsync(orderedIds);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }

            return Ok(ApiResponse.Ok("並び順を更新しました。"));
        }

        private static bool TryParseReserveId(string? id, out Ulid reserveId)
        {
            reserveId = default;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return Ulid.TryParse(id, out reserveId);
        }
    }
}
