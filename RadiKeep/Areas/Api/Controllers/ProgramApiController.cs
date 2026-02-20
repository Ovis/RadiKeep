using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Context;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Logics.PlayProgramLogic;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.ReserveLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Areas.Api.Controllers
{
    [Area("api")]
    [ApiController]
    [Route("/api/v1/programs")]
    public class ProgramApiController(
        ILogger<ProgramApiController> logger,
        IRadioAppContext appContext,
        IAppConfigurationService config,
        StationLobLogic stationLobLogic,
        RadikoUniqueProcessLogic radikoUniqueProcessLogic,
        ProgramScheduleLobLogic programScheduleLobLogic,
        ReserveLobLogic reserveLobLogic,
        PlayProgramLobLogic playProgramLobLogic,
        IEntryMapper entryMapper) : ControllerBase
    {
        /// <summary>
        /// 現在エリアの放送局一覧を取得する
        /// </summary>
        /// <returns>取得失敗時は空配列</returns>
        private async ValueTask<List<string>> GetCurrentAreaStationsAsync()
        {
            var (isSuccess, area) = await radikoUniqueProcessLogic.GetRadikoAreaAsync();
            if (!isSuccess || string.IsNullOrWhiteSpace(area))
            {
                logger.ZLogWarning($"radikoエリア情報の取得に失敗");
                return [];
            }

            return await stationLobLogic.GetCurrentAreaStations(area);
        }
        [HttpGet]
        [Route("stations/radiko")]
        public async ValueTask<IActionResult> GetRadikoStation()
        {
            var stations = (await stationLobLogic.GetRadikoStationAsync()).GroupBy(r => r.RegionId)
                .OrderBy(r => r.First().RegionOrder)
                .ToDictionary(r => r.First().RegionName, r => r.OrderBy(s => s.StationOrder).ToList());

            return Ok(ApiResponse.Ok(stations));
        }



        [HttpGet]
        [Route("stations/radiru")]
        public IActionResult GetNhkStation()
        {
            var stations = stationLobLogic.GetRadiruStationAsync()
                .GroupBy(r => r.AreaName)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            return Ok(ApiResponse.Ok(stations));
        }

        [HttpGet]
        [Route("list/radiko")]
        public async ValueTask<IActionResult> GetRadikoProgram([FromQuery] string d, [FromQuery] string s)
        {
            // d をDateTimeに変換 フォーマットはyyyyMMdd
            if (!DateOnly.TryParseExact(d, "yyyyMMdd", null, DateTimeStyles.None, out var date))
            {
                logger.ZLogWarning($"日付の変換に失敗 {d}", d);
                return BadRequest(ApiResponse.Fail("日付の変換に失敗しました。"));
            }

            if (!config.IsRadikoAreaFree)
            {
                var currentAreaStations = await GetCurrentAreaStationsAsync();
                var currentAreaStationSet = currentAreaStations.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (currentAreaStationSet.Count == 0 || !currentAreaStationSet.Contains(s))
                {
                    return Ok(ApiResponse.Ok(new List<RadioProgramEntry>()));
                }
            }

            var programs = await programScheduleLobLogic.GetRadikoProgramListAsync(date, s);
            return Ok(ApiResponse.Ok(programs));
        }




        [HttpGet]
        [Route("list/radiru")]
        public async ValueTask<IActionResult> GetNhkProgram([FromQuery] string d, [FromQuery] string s, [FromQuery] string a)
        {
            // d をDateTimeに変換 フォーマットはyyyyMMdd
            if (!DateOnly.TryParseExact(d, "yyyyMMdd", null, DateTimeStyles.None, out var date))
            {
                logger.ZLogWarning($"日付の変換に失敗 {d}", d);
                return BadRequest(ApiResponse.Fail("日付の変換に失敗しました。"));
            }


            var programs = await programScheduleLobLogic.GetRadiruProgramAsync(date, a, s);
            return Ok(ApiResponse.Ok(programs));
        }



        [HttpGet]
        [Route("now")]
        public async ValueTask<IActionResult> GetNowOnAirProgram()
        {
            var radikoPrograms = await programScheduleLobLogic
                .GetRadikoNowOnAirProgramListAsync(appContext.StandardDateTimeOffset);
            var radiruPrograms = await programScheduleLobLogic
                .GetRadiruNowOnAirProgramListAsync(appContext.StandardDateTimeOffset);

            var programs = radikoPrograms
                .Concat(radiruPrograms)
                .OrderBy(r => r.StationName)
                .ToList();
            List<string> currentAreaStationsForUi = [];

            var stationList = await stationLobLogic.GetAllRadikoStationAsync();
            var stationById = stationList.ToDictionary(r => r.StationId, r => r);
            var regionOrderMap = stationList
                .GroupBy(r => r.RegionId)
                .ToDictionary(
                    r => r.Key,
                    r => new
                    {
                        RegionName = r.First().RegionName,
                        RegionOrder = r.Min(s => s.RegionOrder)
                    });

            foreach (var program in programs)
            {
                if (stationById.TryGetValue(program.StationId, out var station))
                {
                    program.AreaId = station.RegionId;
                    program.AreaName = station.RegionName;
                }
            }

            if (!config.IsRadikoAreaFree)
            {
                currentAreaStationsForUi = await GetCurrentAreaStationsAsync();
                var currentAreaStationSet = currentAreaStationsForUi.ToHashSet(StringComparer.OrdinalIgnoreCase);
                programs = programs
                    .Where(p =>
                        p.ServiceKind == RadioServiceKind.Radiru ||
                        currentAreaStationSet.Contains(p.StationId))
                    .ToList();
            }

            var radiruAreaOrderMap = Enum
                .GetValues<RadiruAreaKind>()
                .Select((area, index) => new { AreaId = area.GetEnumCodeId(), AreaOrder = index })
                .ToDictionary(x => x.AreaId, x => x.AreaOrder);

            var areaList = programs
                .Where(p => !string.IsNullOrWhiteSpace(p.AreaId) && !string.IsNullOrWhiteSpace(p.AreaName))
                .GroupBy(p => new { p.ServiceKind, p.AreaId, p.AreaName })
                .Select(group =>
                {
                    var areaId = group.Key.AreaId;
                    var areaName = group.Key.AreaName;
                    var serviceKind = group.Key.ServiceKind;

                    if (serviceKind == RadioServiceKind.Radiko && regionOrderMap.TryGetValue(areaId, out var region))
                    {
                        return new
                        {
                            AreaId = areaId,
                            AreaName = region.RegionName,
                            AreaOrder = region.RegionOrder,
                            ServiceOrder = 0
                        };
                    }

                    if (serviceKind == RadioServiceKind.Radiru && radiruAreaOrderMap.TryGetValue(areaId, out var radiruOrder))
                    {
                        return new
                        {
                            AreaId = areaId,
                            AreaName = areaName,
                            AreaOrder = radiruOrder,
                            ServiceOrder = 1
                        };
                    }

                    return new
                    {
                        AreaId = areaId,
                        AreaName = areaName,
                        AreaOrder = int.MaxValue,
                        ServiceOrder = 9
                    };
                })
                .OrderBy(r => r.ServiceOrder)
                .ThenBy(r => r.AreaOrder)
                .ThenBy(r => r.AreaName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(ApiResponse.Ok(new
            {
                Programs = programs,
                Areas = areaList,
                CurrentAreaStations = config.IsRadikoAreaFree ? new List<string>() : currentAreaStationsForUi,
                IsAreaFree = config.IsRadikoAreaFree
            }));
        }



        [HttpGet]
        [Route("detail")]
        public async ValueTask<IActionResult> GetProgramData([FromQuery] string? id, [FromQuery] string? kind)
        {
            if (id == null || kind == null)
            {
                return BadRequest(ApiResponse.Fail("必要な項目が記載されていません。"));
            }

            var radioServiceKind = kind.GetEnumByCodeId<RadioServiceKind>();

            if (radioServiceKind == RadioServiceKind.Undefined)
            {
                return BadRequest(ApiResponse.Fail("サービス種別が不正です。"));
            }

            var program = await programScheduleLobLogic.GetProgramAsync(id, radioServiceKind);
            return Ok(ApiResponse.Ok(program));
        }


        /// <summary>
        /// 番組表キーワード検索
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("search")]
        public async ValueTask<IActionResult> SearchProgram(ProgramSearchEntity entity)
        {
            var result = new List<ProgramForApiEntry>();

            // radiko
            if (entity.SelectedRadikoStationIds.Any())
            {
                if (!config.IsRadikoAreaFree)
                {
                    var currentAreaStations = await GetCurrentAreaStationsAsync();
                    var currentAreaStationSet = currentAreaStations.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    entity.SelectedRadikoStationIds =
                        entity.SelectedRadikoStationIds
                            .Where(id => currentAreaStationSet.Contains(id))
                            .ToList();
                }

                if (entity.SelectedRadikoStationIds.Any())
                {
                    var list = await programScheduleLobLogic.SearchRadikoProgramAsync(entity);

                    result.AddRange(list.Select(entryMapper.ToRadikoProgramForApiEntry));
                }
            }

            // らじる★らじる
            if (entity.SelectedRadiruStationIds.Any())
            {
                var list = await programScheduleLobLogic.SearchRadiruProgramAsync(entity);

                result.AddRange(list.Select(entryMapper.ToRadiruProgramForApiEntry));
            }

            result = entity.OrderKind switch
            {
                KeywordReserveOrderKind.ProgramStartDateTimeAsc => result.OrderBy(x => x.StartTime).ToList(),
                KeywordReserveOrderKind.ProgramStartDateTimeDesc => result.OrderByDescending(x => x.StartTime).ToList(),
                KeywordReserveOrderKind.ProgramEndDateTimeAsc => result.OrderBy(x => x.EndTime).ToList(),
                KeywordReserveOrderKind.ProgramEndDateTimeDesc => result.OrderByDescending(x => x.EndTime).ToList(),
                KeywordReserveOrderKind.ProgramNameAsc => result.OrderBy(x => x.Title).ToList(),
                KeywordReserveOrderKind.ProgramNameDesc => result.OrderByDescending(x => x.Title).ToList(),
                _ => result
            };

            // 件数が多い場合は100件に絞る
            return Ok(ApiResponse.Ok(result.Take(100)));
        }


        /// <summary>
        /// キーワード予約
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("keyword-reserve")]
        public async ValueTask<IActionResult> SetKeywordReserve(KeywordReserveEntry entry)
        {
            if (!Enum.IsDefined(typeof(KeywordReserveTagMergeBehavior), entry.MergeTagBehavior))
            {
                return BadRequest(ApiResponse.Fail("タグマージ設定が不正です。"));
            }

            if (!string.IsNullOrEmpty(entry.RecordPath) && !entry.RecordPath.IsValidRelativePath())
            {
                return BadRequest(ApiResponse.Fail("保存先パスが不正です。"));
            }

            if (!string.IsNullOrEmpty(entry.RecordFileName) && !entry.RecordFileName.IsValidFileName())
            {
                return BadRequest(ApiResponse.Fail("ファイル名が不正です。"));
            }

            var (isSuccess, error) = await reserveLobLogic.SetKeywordReserveAsync(entry);
            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error?.Message ?? "キーワード予約の登録に失敗しました。"));
            }

            return Ok(ApiResponse.Ok("登録しました。"));
        }



        [HttpPost]
        [Route("update")]
        public async ValueTask<IActionResult> UpdateProgram()
        {
            var (isSuccess, error) = await programScheduleLobLogic.ScheduleImmediateUpdateProgramJobAsync();

            if (!isSuccess)
            {
                return BadRequest(ApiResponse.Fail(error!.Message));
            }
            else
            {
                return Ok(ApiResponse.Ok("番組表更新処理を実行中です。通常数分で更新が完了します。"));
            }
        }


        [HttpPost]
        [Route("reserve")]
        public async ValueTask<IActionResult> RecordRadikoProgram(ProgramInformationRequestEntry program)
        {
            switch (program.RadioServiceKind)
            {
                case RadioServiceKind.Radiko:
                    {
                        var (isSuccess, error) = await reserveLobLogic.SetRecordingJobByProgramIdAsync(program.ProgramId, RadioServiceKind.Radiko, program.RecordingType);

                        if (!isSuccess)
                        {
                            return BadRequest(ApiResponse.Fail(error?.Message ?? "録音予約に失敗しました。"));
                        }
                    }
                    break;
                case RadioServiceKind.Radiru:
                    {
                        var (isSuccess, error) = await reserveLobLogic.SetRecordingJobByProgramIdAsync(program.ProgramId, RadioServiceKind.Radiru, program.RecordingType);

                        if (!isSuccess)
                        {
                            return BadRequest(ApiResponse.Fail(error?.Message ?? "録音予約に失敗しました。"));
                        }
                    }
                    break;
                case RadioServiceKind.Other:
                case RadioServiceKind.Undefined:
                default:
                    return BadRequest(ApiResponse.Fail("サービス種別が不正です。"));
            }

            return Ok(ApiResponse.Ok("予約しました。"));
        }

        [HttpPost]
        [Route("play")]
        public async ValueTask<IActionResult> PlayProgram(ProgramInformationRequestEntry program)
        {
            switch (program.RadioServiceKind)
            {
                case RadioServiceKind.Radiko:
                    {
                        var (isSuccess, token, url, error) = await playProgramLobLogic.PlayRadikoProgramAsync(program.ProgramId);

                        if (!isSuccess)
                        {
                            return BadRequest(ApiResponse.Fail(error?.Message ?? "再生準備に失敗しました。"));
                        }

                        var response =
                            new
                            {
                                Token = token,
                                Url = url
                            };

                        return Ok(ApiResponse.Ok(response));
                    }
                case RadioServiceKind.Radiru:
                    {
                        var (isSuccess, token, url, error) = await playProgramLobLogic.PlayRadiruProgramAsync(program.ProgramId);

                        if (!isSuccess)
                        {
                            return BadRequest(ApiResponse.Fail(error?.Message ?? "番組の再生ができませんでした。"));
                        }

                        var response =
                            new
                            {
                                Token = token,
                                Url = url
                            };

                        return Ok(ApiResponse.Ok(response));
                    }
                case RadioServiceKind.Other:
                case RadioServiceKind.Undefined:
                default:
                    return BadRequest(ApiResponse.Fail("サービス種別が不正です。"));
            }
        }

    }
}
