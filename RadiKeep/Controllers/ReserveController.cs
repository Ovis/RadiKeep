using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Models;

namespace RadiKeep.Controllers
{
    public class ReserveController(
        StationLobLogic stationLobLogic) : Controller
    {
        public IActionResult ProgramReserveList()
        {
            return View();
        }

        public async ValueTask<IActionResult> KeywordReserveList()
        {
            var radikoStationList = await stationLobLogic.GetRadikoStationAsync();

            var radiruStationList = stationLobLogic.GetRadiruStationAsync();

            return View(new KeywordReserveViewModel
            {
                RadikoStationList = radikoStationList,
                RadiruStationList = radiruStationList
            });
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
