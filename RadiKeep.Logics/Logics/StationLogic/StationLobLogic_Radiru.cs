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
        public async ValueTask<IEnumerable<RadiruStationEntry>> GetRadiruStationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var stationEntries = await stationRepository.GetRadiruStationsFromAreaServicesAsync(cancellationToken);
                if (stationEntries.Count > 0)
                {
                    return stationEntries
                        .Select(entry => new RadiruStationEntry
                        {
                            AreaId = entry.AreaId,
                            AreaName = entry.AreaName,
                            StationId = entry.StationId,
                            StationName = string.IsNullOrWhiteSpace(entry.StationName)
                                ? ResolveRadiruStationName(entry.StationId)
                                : entry.StationName
                        })
                        .GroupBy(x => $"{x.AreaId}:{x.StationId}", StringComparer.OrdinalIgnoreCase)
                        .Select(x => x.First())
                        .ToList();
                }
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる★らじる局一覧の新テーブル読込に失敗したため固定定義にフォールバックします。");
            }

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
            List<NhkRadiruArea> areaDefinitions;
            List<NhkRadiruAreaService> serviceDefinitions;
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

                var programNowOnAirUrlTemplate = GetDescendantValue(doc, "url_program_noa");
                var programDetailApiUrlTemplate = GetDescendantValue(doc, "url_program_detail");
                var dailyProgramApiUrlTemplate = GetDescendantValue(doc, "url_program_day");
                var syncedAtUtc = DateTimeOffset.UtcNow;

                stationList = [];
                areaDefinitions = [];
                serviceDefinitions = [];

                foreach (var data in doc.Descendants("stream_url").Descendants("data"))
                {
                    var areaId = GetDescendantValue(data, "areakey");
                    if (string.IsNullOrWhiteSpace(areaId))
                    {
                        logger.ZLogWarning($"らじる★らじる設定XMLで areakey が空のためスキップしました。");
                        continue;
                    }

                    var areaName = GetDescendantValue(data, "areajp");
                    var apiKey = GetDescendantValue(data, "apikey");
                    var areaNoaUrl = programNowOnAirUrlTemplate.Replace("{area}", areaId).ToHttpsUrl();
                    var areaDetailUrl = programDetailApiUrlTemplate.Replace("{area}", areaId).ToHttpsUrl();
                    var areaDailyUrl = dailyProgramApiUrlTemplate.Replace("{area}", areaId).ToHttpsUrl();

                    var serviceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var element in data.Elements())
                    {
                        var tagName = element.Name.LocalName;
                        if (!tagName.EndsWith("hls", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var serviceId = ConvertHlsTagNameToServiceId(tagName);
                        var hlsUrl = (element.Value ?? string.Empty).Trim().ToHttpsUrl();
                        if (string.IsNullOrWhiteSpace(serviceId) || string.IsNullOrWhiteSpace(hlsUrl))
                        {
                            continue;
                        }

                        serviceMap[serviceId] = hlsUrl;

                        serviceDefinitions.Add(new NhkRadiruAreaService
                        {
                            AreaId = areaId,
                            ServiceId = serviceId,
                            ServiceName = ResolveRadiruStationName(serviceId),
                            HlsUrl = hlsUrl,
                            IsActive = true,
                            SourceTag = tagName.ToLowerInvariant(),
                            LastSyncedAtUtc = syncedAtUtc
                        });
                    }

                    if (serviceMap.Count == 0)
                    {
                        logger.ZLogWarning($"らじる★らじる設定XMLでHLSサービスが取得できませんでした。 areaId={areaId}");
                    }

                    areaDefinitions.Add(new NhkRadiruArea
                    {
                        AreaId = areaId,
                        AreaJpName = areaName,
                        ApiKey = apiKey,
                        ProgramNowOnAirApiUrl = areaNoaUrl,
                        ProgramDetailApiUrlTemplate = areaDetailUrl,
                        DailyProgramApiUrlTemplate = areaDailyUrl,
                        LastSyncedAtUtc = syncedAtUtc
                    });

                    stationList.Add(new NhkRadiruStation
                    {
                        AreaJpName = areaName,
                        AreaId = areaId,
                        ApiKey = apiKey,
                        R1Hls = serviceMap.TryGetValue("r1", out var r1Hls) ? r1Hls : string.Empty,
                        R2Hls = serviceMap.TryGetValue("r2", out var r2Hls) ? r2Hls : string.Empty,
                        FmHls = serviceMap.TryGetValue("r3", out var r3Hls) ? r3Hls : string.Empty,
                        ProgramNowOnAirApiUrl = areaNoaUrl,
                        ProgramDetailApiUrlTemplate = areaDetailUrl,
                        DailyProgramApiUrlTemplate = areaDailyUrl
                    });
                }
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる\u2605らじるの放送局情報取得処理で例外発生");
                throw;
            }


            try
            {
                await stationRepository.UpsertRadiruAreasAndServicesAsync(areaDefinitions, serviceDefinitions);
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

        /// <summary>
        /// 指定エリアとサービスIDかららじる★らじるのHLS URLを取得
        /// </summary>
        /// <param name="areaId">エリアID</param>
        /// <param name="serviceId">サービスID</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>HLS URL。未取得時はnull</returns>
        public async ValueTask<string?> GetRadiruHlsUrlByAreaAndServiceAsync(
            string areaId,
            string serviceId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await stationRepository.GetRadiruHlsUrlByAreaAndServiceAsync(areaId, serviceId, cancellationToken);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる★らじるのHLS URL取得に失敗 areaId={areaId} serviceId={serviceId}");
                throw;
            }
        }

        /// <summary>
        /// らじる★らじるの有効なエリアID/サービスID組を取得
        /// </summary>
        public async ValueTask<List<(string AreaId, string ServiceId)>> GetActiveRadiruAreaServiceKeysAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var keys = await stationRepository.GetActiveRadiruAreaServiceKeysAsync(cancellationToken);
                if (keys.Count > 0)
                {
                    return keys;
                }
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる★らじるサービス定義の取得に失敗したため固定定義にフォールバックします。");
            }

            return Enum.GetValues<RadiruAreaKind>()
                .SelectMany(area => Enumeration.GetAll<RadiruStationKind>()
                    .Select(station => (AreaId: area.GetEnumCodeId(), ServiceId: station.ServiceId)))
                .ToList();
        }

        /// <summary>
        /// 指定エリアの番組表API URLテンプレートを取得
        /// </summary>
        public async ValueTask<string?> GetRadiruDailyProgramApiUrlTemplateAsync(string areaId, CancellationToken cancellationToken = default)
        {
            try
            {
                var area = await stationRepository.GetRadiruAreaByAreaIdAsync(areaId, cancellationToken);
                if (area != null && !string.IsNullOrWhiteSpace(area.DailyProgramApiUrlTemplate))
                {
                    return area.DailyProgramApiUrlTemplate;
                }
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる★らじるエリア定義取得に失敗 areaId={areaId}");
            }

            var legacyArea = Enum.GetValues<RadiruAreaKind>()
                .FirstOrDefault(x => x.GetEnumCodeId() == areaId);

            if (legacyArea.GetEnumCodeId() != areaId)
            {
                return null;
            }

            var legacy = await GetNhkRadiruStationInformationByAreaAsync(legacyArea);
            return legacy.DailyProgramApiUrlTemplate;
        }

        private static string ResolveRadiruStationName(string stationId)
        {
            var station = Enumeration.GetAll<RadiruStationKind>()
                .FirstOrDefault(r => r.ServiceId == stationId);

            return station?.Name ?? $"不明局({stationId})";
        }

        private static string GetDescendantValue(XContainer element, string descendantName)
        {
            return element.Descendants(descendantName).FirstOrDefault()?.Value?.Trim() ?? string.Empty;
        }

        private static string ConvertHlsTagNameToServiceId(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return string.Empty;
            }

            var normalized = tagName.Trim().ToLowerInvariant();
            if (!normalized.EndsWith("hls", StringComparison.Ordinal) || normalized.Length <= 3)
            {
                return string.Empty;
            }

            var baseId = normalized[..^3];
            if (baseId == "fm")
            {
                return "r3";
            }

            return baseId;
        }

    }
}

