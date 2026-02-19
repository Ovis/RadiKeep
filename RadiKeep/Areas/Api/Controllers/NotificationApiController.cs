using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Logics.NotificationLogic;

namespace RadiKeep.Areas.Api.Controllers
{
    [Area("api")]
    [ApiController]
    [Route("/api/v1/notifications")]
    public class NotificationApiController(NotificationLobLogic notificationLobLogic) : ControllerBase
    {
        /// <summary>
        /// 未読のお知らせ件数を取得
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("count")]
        public async ValueTask<IActionResult> GetLatestNotificationCount()
        {
            var count = await notificationLobLogic.GetUnreadNotificationCountAsync();
            return Ok(ApiResponse.Ok(count));
        }

        /// <summary>
        /// 未読のお知らせ一覧を取得
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("latest")]
        public async ValueTask<IActionResult> GetLatestNotification()
        {
            var list = await notificationLobLogic.GetUnreadNotificationListAsync();

            var entry = new
            {
                Count = list.Count,
                // 最新5件だけ返す
                List = list.Take(5)
            };

            return Ok(ApiResponse.Ok(entry));
        }


        /// <summary>
        /// お知らせ一覧取得
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("")]
        public async ValueTask<IActionResult> GetRecordedRadio(int page = 1, int pageSize = 20)
        {
            var (isSuccess, total, list, _) = await notificationLobLogic.GetNotificationListAsync(page, pageSize);

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail("お知らせの取得に失敗しました。"));
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


        /// <summary>
        /// お知らせの削除
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("clear")]
        public async ValueTask<IActionResult> DeleteNotification()
        {
            await notificationLobLogic.DeleteAllNotificationAsync();

            return Ok(ApiResponse.Ok("削除しました。"));
        }
    }
}

