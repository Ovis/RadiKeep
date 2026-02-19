using System.Xml.Linq;
using RadiKeep.Logics.Application;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Primitives;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.RdbContext;
using ZLogger;

namespace RadiKeep.Logics.Logics.StationLogic
{
    public partial class StationLobLogic
    {
        /// <summary>
        /// らじる★らじるの放送局情報が初期化されているかチェック
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> CheckInitializedRadiruRadiruStationAsync()
        {
            try
            {
                var hasStationData = await stationRepository.HasAnyRadiruStationAsync();

                return hasStationData;
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"Failed to check if NhkRadiruStation is initialized.");
                throw;
            }
        }

        /// <summary>
        /// らじる★らじるの放送局情報取得
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RadiruStationEntry> GetRadiruStationAsync()
        {
            var list = new List<RadiruStationEntry>();
            {
                foreach (var areaKind in Enum.GetValues<RadiruAreaKind>())
                {
                    list.AddRange(Enumeration.GetAll<RadiruStationKind>()
                        .Select(
                            radiruStationKind => entryMapper.ToRadiruStationEntry(areaKind, radiruStationKind)));
                }
            }

            return list;
        }


        /// <summary>
        /// らじる★らじるの放送局情報を更新
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> UpdateRadiruStationInformationAsync()
        {
            List<NhkRadiruStation> stationList;
            try
            {
                using var client = httpClientFactory.CreateClient(HttpClientNames.Radiru);
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.nhk.or.jp/radio/config/config_web.xml");
                request.Headers.Accept.ParseAdd("application/xml");
                request.Headers.AcceptLanguage.ParseAdd("ja-JP,ja;q=0.9,en;q=0.8");
                request.Headers.TryAddWithoutValidation("User-Agent", config.ExternalServiceUserAgent);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    logger.ZLogError($"らじる★らじるの設定XML取得に失敗: StatusCode={response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync();
                var doc = await XDocument.LoadAsync(responseStream, LoadOptions.None, CancellationToken.None);

                var programNowOnAirUrlTemplate = doc.Descendants("url_program_noa").First().Value;
                var programDetailApiUrlTemplate = doc.Descendants("url_program_detail").First().Value;
                var dailyProgramApiUrlTemplate = doc.Descendants("url_program_day").First().Value;

                stationList = doc.Descendants("stream_url")
                    .Descendants("data")
                    .Select(data => new NhkRadiruStation
                    {
                        AreaJpName = data.Descendants("areajp").First().Value,
                        AreaId = data.Descendants("areakey").First().Value,
                        ApiKey = data.Descendants("apikey").First().Value,
                        R1Hls = data.Descendants("r1hls").First().Value,
                        R2Hls = data.Descendants("r2hls").First().Value,
                        FmHls = data.Descendants("fmhls").First().Value,
                        ProgramNowOnAirApiUrl = programNowOnAirUrlTemplate.Replace("{area}", data.Descendants("areakey").First().Value).ToHttpsUrl(),
                        ProgramDetailApiUrlTemplate = programDetailApiUrlTemplate.Replace("{area}", data.Descendants("areakey").First().Value.ToHttpsUrl()),
                        DailyProgramApiUrlTemplate = dailyProgramApiUrlTemplate.Replace("{area}", data.Descendants("areakey").First().Value.ToHttpsUrl())

                    })
                    .ToList();
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる\u2605らじるの放送局情報取得処理で例外発生");
                throw;
            }


            try
            {
                await stationRepository.UpsertRadiruStationsAsync(stationList);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる★らじるの放送局情報更新に失敗");
                throw;
            }

            return true;
        }


        /// <summary>
        /// らじる★らじるで指定された地域の放送局情報を取得
        /// </summary>
        /// <param name="areaKind"></param>
        /// <returns></returns>
        public async ValueTask<NhkRadiruStation> GetNhkRadiruStationInformationByAreaAsync(RadiruAreaKind areaKind)
        {
            NhkRadiruStation station;
            try
            {
                station = await stationRepository.GetRadiruStationByAreaAsync(areaKind.GetEnumCodeId());
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる★らじるの放送局情報取得に失敗");
                throw;
            }

            return station;
        }

    }
}

