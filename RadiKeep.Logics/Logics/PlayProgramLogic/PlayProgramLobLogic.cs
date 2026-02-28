using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.RdbContext;
using ZLogger;

namespace RadiKeep.Logics.Logics.PlayProgramLogic
{
    public class PlayProgramLobLogic(
        ILogger<RecordingLobLogic> logger,
        RadioDbContext dbContext,
        RadikoUniqueProcessLogic radikoUniqueProcessLogic,
        ProgramScheduleLobLogic programScheduleLobLogic,
        StationLobLogic stationLobLogic
        )
    {
        /// <summary>
        /// radikoの番組を再生するためのストリームURL取得
        /// </summary>
        /// <param name="programId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async ValueTask<(bool IsSuccess, string? Token, string? Url, Exception? Error)>
            PlayRadikoProgramAsync(string programId)
        {
            string area;

            // RadikoProgramsテーブルからidに一致するデータを取得
            // そのデータを元に録音処理を行う
            var program = await programScheduleLobLogic.GetRadikoProgramAsync(programId);

            if (program == null)
            {
                logger.ZLogError($"再生処理において番組情報の取得に失敗");
                return (false, null, null, new DomainException("番組情報の取得に失敗しました。"));
            }

            var now = DateTimeOffset.UtcNow;
            if (program.StartTime > now)
            {
                return (false, null, null, new DomainException("この番組はまだ放送前のため再生できません。"));
            }

            if (program.EndTime <= now)
            {
                return (false, null, null, new DomainException("この番組はすでに放送終了しているため再生できません。"));
            }

            {
                var (isSuccess, areaString) = await radikoUniqueProcessLogic.GetRadikoAreaAsync();

                if (!isSuccess)
                {
                    logger.ZLogError($"再生処理においてradikoエリア情報の取得に失敗");
                    return (false, null, null, new DomainException("エリア情報の取得に失敗しました。"));
                }
                area = areaString;
            }

            string loginSession;
            bool isAreaFree;
            {
                var (_, loginSessionString, isPremiumUser, currentIsAreaFree) = await radikoUniqueProcessLogic.LoginRadikoAsync();

                var stationInformation = await dbContext.RadikoStations.FindAsync(program.StationId);

                var currentAreaStation = await stationLobLogic.GetCurrentAreaStations(area);

                if (!currentAreaStation.Contains(stationInformation!.StationId) && !isPremiumUser)
                {
                    logger.ZLogError($"現在のエリアと番組の視聴可能エリアが異なる。現在エリア{string.Join(',', currentAreaStation)} 番組視聴エリア {stationInformation.StationId}");
                    return (false, null, null, new DomainException("この番組は地域が異なるため再生できませんでした。異なる地域の番組を再生する場合はプレミアム会員としてログインする必要があります。"));
                }

                loginSession = loginSessionString;
                isAreaFree = currentIsAreaFree;
            }

            var (authSuccess, token, _) = await radikoUniqueProcessLogic.AuthorizeRadikoAsync(loginSession);
            if (!authSuccess || string.IsNullOrWhiteSpace(token))
            {
                logger.ZLogError($"再生処理においてradiko認証に失敗");
                return (false, null, null, new DomainException("radiko認証に失敗しました。"));
            }

            var streamUrl = $"https://f-radiko.smartstream.ne.jp/{program.StationId}/_definst_/simul-stream.stream/playlist.m3u8";

            return (true, token, streamUrl, null);
        }

        /// <summary>
        /// らじる★らじるの番組を再生するためのストリームURL取得
        /// </summary>
        /// <param name="programId"></param>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, string? Token, string? Url, Exception? Error)>
            PlayRadiruProgramAsync(string programId)
        {
            var program = await dbContext.NhkRadiruPrograms.FindAsync(programId);
            if (program == null)
            {
                logger.ZLogError($"らじる再生処理で番組情報の取得に失敗 programId={programId}");
                return (false, null, null, new DomainException("番組の再生ができませんでした。"));
            }

            var now = DateTimeOffset.UtcNow;
            if (program.StartTime > now)
            {
                return (false, null, null, new DomainException("この番組はまだ放送前のため再生できません。"));
            }

            if (program.EndTime <= now)
            {
                return (false, null, null, new DomainException("この番組はすでに放送終了しているため再生できません。"));
            }

            var station = await dbContext.NhkRadiruStations
                .FirstOrDefaultAsync(s => s.AreaId == program.AreaId);
            if (station == null)
            {
                logger.ZLogError($"らじる再生処理で局情報の取得に失敗 programId={programId} areaId={program.AreaId} stationId={program.StationId}");
                return (false, null, null, new DomainException("番組の再生ができませんでした。"));
            }

            var streamUrl = program.StationId.ToLowerInvariant() switch
            {
                var id when id == RadiruStationKind.R1.ServiceId => station.R1Hls,
                var id when id == RadiruStationKind.R2.ServiceId => station.R2Hls,
                var id when id == RadiruStationKind.FM.ServiceId => station.FmHls,
                _ => string.Empty
            };

            if (!IsValidRadiruHlsUrl(streamUrl))
            {
                logger.ZLogError($"らじる再生処理でHLS URLが不正 programId={programId} areaId={program.AreaId} stationId={program.StationId}");
                return (false, null, null, new DomainException("番組の再生ができませんでした。"));
            }

            return (true, null, streamUrl, null);
        }

        private static bool IsValidRadiruHlsUrl(string? value)
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
    }
}
