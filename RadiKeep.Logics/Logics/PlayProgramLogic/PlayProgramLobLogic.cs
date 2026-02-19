using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.RecordingLogic;
using RadiKeep.Logics.Logics.StationLogic;
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
            {
                var (_, loginSessionString, isPremiumUser, _) = await radikoUniqueProcessLogic.LoginRadikoAsync();

                var stationInformation = await dbContext.RadikoStations.FindAsync(program.StationId);

                var currentAreaStation = await stationLobLogic.GetCurrentAreaStations(area);

                if (!currentAreaStation.Contains(stationInformation!.StationId) && !isPremiumUser)
                {
                    logger.ZLogError($"現在のエリアと番組の視聴可能エリアが異なる。現在エリア{string.Join(',', currentAreaStation)} 番組視聴エリア {stationInformation.StationId}");
                    return (false, null, null, new DomainException("この番組は地域が異なるため再生できませんでした。異なる地域の番組を再生する場合はプレミアム会員としてログインする必要があります。"));
                }

                loginSession = loginSessionString;
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
    }
}
