using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Models.NhkRadiru.JsonEntity;
using RadiKeep.Logics.Primitives;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.RdbContext;
using ZLogger;

namespace RadiKeep.Logics.Logics.ProgramScheduleLogic
{
    public partial class ProgramScheduleLobLogic
    {
        /// <summary>
        /// 指定時刻に放送されているらじる★らじるの番組表情報リストを取得
        /// </summary>
        /// <returns></returns>
        public async ValueTask<List<RadioProgramEntry>> GetRadiruNowOnAirProgramListAsync(DateTimeOffset standardDateTimeOffset)
        {
            var list = await programScheduleRepository.GetRadiruNowOnAirAsync(standardDateTimeOffset);
            return list.Select(entryMapper.ToRadioProgramEntry).ToList();
        }

        /// <summary>
        /// らじる★らじるの番組表情報を取得
        /// </summary>
        /// <returns></returns>
        public async ValueTask<List<RadioProgramEntry>> GetRadiruProgramAsync(DateOnly date, string areaId, string stationId)
        {
            var list = await programScheduleRepository.GetRadiruProgramsAsync(date, areaId, stationId);

            return list.Select(entryMapper.ToRadioProgramEntry).ToList();
        }


        /// <summary>
        /// らじる★らじるの番組情報を取得
        /// </summary>
        /// <returns></returns>
        public async ValueTask<RadioProgramEntry?> GetRadiruProgramAsync(string programId)
        {
            var program = await programScheduleRepository.GetRadiruProgramByIdAsync(programId);

            if (program == null)
            {
                return null;
            }

            return entryMapper.ToRadioProgramEntry(program);
        }



        public async ValueTask UpdateRadiruProgramDataAsync()
        {
            var radiruStationKindList = Enumeration.GetAll<RadiruStationKind>().ToList();
            var dateList = Enumerable.Range(-7, 15)
                .Select(i => appContext.StandardDateTimeOffset.AddDays(-i))
                .ToList();

            // RadiruAreaKind を foreachで回して、それぞれのエリアの放送局情報を取得
            try
            {
                foreach (var recArea in Enum.GetValues<RadiruAreaKind>())
                {
                    foreach (var radiruStationKind in radiruStationKindList)
                    {
                        foreach (var dateTimeOffset in dateList)
                        {
                            await UpsertDailyProgramDataAsync(recArea, radiruStationKind, dateTimeOffset);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる\u2605らじるの番組表情報更新で例外発生");
                throw;
            }
        }


        public async ValueTask DeleteOldRadiruProgramAsync()
        {
            try
            {
                var deleteDate = appContext.StandardDateTimeOffset.AddMonths(-1).ToRadioDate();
                await programScheduleRepository.DeleteOldRadiruProgramsAsync(deleteDate);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる\u2605らじるの過去の番組データ削除に失敗");
            }
        }


        private async ValueTask<bool> UpsertDailyProgramDataAsync(RadiruAreaKind area, RadiruStationKind stationKind, DateTimeOffset dt)
        {
            var programList = await radiruApiClient.GetDailyProgramsAsync(area, stationKind, dt);

            if (!programList.Any())
            {
                return false;
            }

            try
            {
                var entries = new List<NhkRadiruProgram>();

                foreach (var programJsonEntity in programList)
                {
                    var onDemandContentUrl = SelectOnDemandContentUrl(programJsonEntity.About.Audio);
                    var onDemandExpiresAtUtc = programJsonEntity.About.Audio.Expires == default
                        ? (DateTime?)null
                        : programJsonEntity.About.Audio.Expires.UtcDateTime;

                    var entry = new NhkRadiruProgram
                    {
                        ProgramId = $"{programJsonEntity.Id}",
                        StationId = stationKind.ServiceId,
                        AreaId = $"{area.GetEnumCodeId()}",
                        RadioDate = programJsonEntity.StartDate.ToRadioDate(),
                        DaysOfWeek = programJsonEntity.StartDate.ToRadioDayOfWeek().ToDaysOfWeek(),
                        EventId = programJsonEntity.About.Id,
                        StartTime = programJsonEntity.StartDate,
                        EndTime = programJsonEntity.EndDate,
                        Title = programJsonEntity.GetTitle(),
                        Subtitle = programJsonEntity.IdentifierGroup.RadioEpisodeName.ToSafeName().To半角英数字(),
                        Description = programJsonEntity.GetCombinedDescription(),
                        Performer = programJsonEntity.GetCombinedActorsAndArtists(),
                        SiteId = programJsonEntity.IdentifierGroup.SiteId,
                        ImageUrl = programJsonEntity.About.PartOfSeries.Logo.Medium.Url,
                        ProgramUrl = programJsonEntity.About.Url,
                        OnDemandContentUrl = onDemandContentUrl,
                        OnDemandExpiresAtUtc = onDemandExpiresAtUtc
                    };

                    entries.Add(entry);
                }

                await programScheduleRepository.UpsertRadiruProgramsAsync(entries);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"番組表更新処理に失敗");
                throw;
            }

            return true;
        }

        private static string? SelectOnDemandContentUrl(Audio audio)
        {
            var detailedContents = audio.DetailedContent
                .Where(d => !string.IsNullOrWhiteSpace(d.ContentUrl))
                .ToList();

            if (detailedContents.Count == 0)
            {
                return null;
            }

            var prioritized = detailedContents.FirstOrDefault(d =>
                string.Equals(d.Name, "hls_widevine", StringComparison.OrdinalIgnoreCase) &&
                IsM3u8Url(d.ContentUrl));
            if (prioritized is not null)
            {
                return prioritized.ContentUrl;
            }

            var fallback = detailedContents.FirstOrDefault(d => IsM3u8Url(d.ContentUrl));
            return fallback?.ContentUrl;
        }

        private static bool IsM3u8Url(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// らじる★らじる番組表検索
        /// </summary>
        /// <param name="searchEntity"></param>
        /// <returns></returns>
        public async ValueTask<List<NhkRadiruProgram>> SearchRadiruProgramAsync(ProgramSearchEntity searchEntity)
        {
            try
            {
                return await programScheduleRepository.SearchRadiruProgramsAsync(searchEntity, appContext.StandardDateTimeOffset);
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"番組検索に失敗しました。");
                return [];
            }
        }
    }
}
