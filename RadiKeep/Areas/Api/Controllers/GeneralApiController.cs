using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Extensions;

namespace RadiKeep.Areas.Api.Controllers
{
    [Area("api")]
    [ApiController]
    [Route("/api/v1/general")]
    public class GeneralApiController(IRadioAppContext context) : ControllerBase
    {
        /// <summary>
        /// 番組表で選択可能な日付を取得
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("radio-dates")]
        public IActionResult GetRadioDates()
        {
            var today = context.StandardDateTimeOffset.ToRadioDate();

            var dates = Enumerable.Range(-7, 14)
                .Select(i =>
                {
                    var date = today.AddDays(i);
                    return new
                    {
                        Value = date.ToString("yyyyMMdd"),
                        TextContent = date.ToString("yyyy/MM/dd(ddd)", context.CultureInfo),
                        IsToday = date == today
                    };
                })
                .ToList();

            return Ok(ApiResponse.Ok(dates));
        }
    }
}
