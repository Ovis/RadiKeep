using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Interfaces;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.RdbContext;
using ZLogger;

namespace RadiKeep.Logics.Infrastructure.Recording;

/// <summary>
/// radiko録音用のソース取得実装
/// </summary>
public class RadikoRecordingSource(
    ILogger<RadikoRecordingSource> logger,
    ProgramScheduleLobLogic programScheduleLobLogic,
    StationLobLogic stationLobLogic,
    RadikoUniqueProcessLogic radikoUniqueProcessLogic,
    IRadikoApiClient radikoApiClient,
    RadioDbContext dbContext) : IRecordingSource
{
    /// <summary>
    /// radikoを処理可能か判定する
    /// </summary>
    public bool CanHandle(RadioServiceKind kind) => kind == RadioServiceKind.Radiko;

    /// <summary>
    /// 録音に必要なストリームURLと番組情報を取得する
    /// </summary>
    public async ValueTask<RecordingSourceResult> PrepareAsync(RecordingCommand command, CancellationToken cancellationToken = default)
    {
        // 番組情報の取得
        var program = await programScheduleLobLogic.GetRadikoProgramAsync(command.ProgramId);
        if (program == null)
        {
            throw new DomainException("番組情報の取得に失敗しました。");
        }

        // 現在エリア取得
        var (areaSuccess, area) = await radikoUniqueProcessLogic.GetRadikoAreaAsync();
        if (!areaSuccess)
        {
            throw new DomainException("エリア情報の取得に失敗しました。");
        }

        // ログインとエリアチェック
        var (_, session, _, isAreaFree) = await radikoUniqueProcessLogic.LoginRadikoAsync();
        var stationInformation = await dbContext.RadikoStations.FindAsync(program.StationId);
        var currentAreaStation = await stationLobLogic.GetCurrentAreaStations(area);
        if (stationInformation != null && !currentAreaStation.Contains(stationInformation.StationId) && !isAreaFree)
        {
            logger.ZLogError($"現在エリアと番組の視聴可能エリアが異なる。現在エリア{string.Join(',', currentAreaStation)} 番組視聴エリア {stationInformation.StationId}");
            throw new DomainException("この番組は地域が異なるため録音できません。プレミアム会員でのログインが必要です。");
        }

        // 認証トークン取得
        var (authSuccess, token, areaId) = await radikoUniqueProcessLogic.AuthorizeRadikoAsync(session);
        if (!authSuccess || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(areaId))
        {
            throw new DomainException("radiko認証に失敗しました。");
        }

        var headers = new Dictionary<string, string>
        {
            { "X-Radiko-Authtoken", token },
            { "X-Radiko-AreaId", areaId }
        };

        // タイムフリーとリアルタイムでURLを切り替える
        string streamUrl;
        if (command.IsTimeFree)
        {
            var urls = await radikoApiClient.GetTimeFreePlaylistCreateUrlsAsync(program.StationId, isAreaFree, cancellationToken);
            if (urls.Count == 0 && isAreaFree)
            {
                // areafree URLが取得できない場合はfallbackで通常URLを試す
                urls = await radikoApiClient.GetTimeFreePlaylistCreateUrlsAsync(program.StationId, false, cancellationToken);
            }

            if (urls.Count == 0)
            {
                throw new DomainException("タイムフリー録音URLの取得に失敗しました。");
            }

            streamUrl = urls[0];
        }
        else
        {
            streamUrl = $"https://f-radiko.smartstream.ne.jp/{program.StationId}/_definst_/simul-stream.stream/playlist.m3u8";
        }

        var programInfo = new ProgramRecordingInfo(
            ProgramId: program.ProgramId,
            Title: program.Title,
            Subtitle: program.Subtitle,
            StationId: program.StationId,
            StationName: program.StationName,
            AreaId: area,
            StartTime: program.StartTime,
            EndTime: program.EndTime,
            Performer: program.Performer,
            Description: program.Description,
            ProgramUrl: program.ProgramUrl,
            ImageUrl: program.ImageUrl);

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiko,
            IsTimeFree: command.IsTimeFree,
            IsOnDemand: false,
            StartDelaySeconds: command.StartDelaySeconds,
            EndDelaySeconds: command.EndDelaySeconds);

        return new RecordingSourceResult(
            StreamUrl: streamUrl,
            Headers: headers,
            ProgramInfo: programInfo,
            Options: options);
    }
}
