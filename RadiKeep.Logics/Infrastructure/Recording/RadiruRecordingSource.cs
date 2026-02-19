using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Domain.Recording;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.StationLogic;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Primitives;
using RadiKeep.Logics.Primitives.DataAnnotations;
using ZLogger;

namespace RadiKeep.Logics.Infrastructure.Recording;

/// <summary>
/// らじる★らじる録音用のソース取得実装
/// </summary>
public class RadiruRecordingSource(
    ILogger<RadiruRecordingSource> logger,
    ProgramScheduleLobLogic programScheduleLobLogic,
    StationLobLogic stationLobLogic) : IRecordingSource
{
    /// <summary>
    /// らじる★らじるを処理可能か判定する
    /// </summary>
    public bool CanHandle(RadioServiceKind kind) => kind == RadioServiceKind.Radiru;

    /// <summary>
    /// 録音に必要なストリームURLと番組情報を取得する
    /// </summary>
    public async ValueTask<RecordingSourceResult> PrepareAsync(RecordingCommand command, CancellationToken cancellationToken = default)
    {
        // 番組情報の取得
        var program = await programScheduleLobLogic.GetRadiruProgramAsync(command.ProgramId);
        if (program == null)
        {
            logger.ZLogWarning($"番組情報の取得に失敗しました。");
            throw new DomainException("番組情報の取得に失敗しました。");
        }

        var now = DateTimeOffset.UtcNow;
        var isOnDemand = command.IsOnDemand;

        // 通常録音は放送終了後に実行できない
        if (!isOnDemand && program.EndTime < now)
        {
            logger.ZLogWarning($"番組が放送終了しているため録音できません。");
            throw new DomainException("番組が放送終了しているため録音できません。");
        }

        string streamUrl;
        if (isOnDemand)
        {
            if (string.IsNullOrWhiteSpace(program.OnDemandContentUrl))
            {
                logger.ZLogWarning($"聞き逃し配信URLが存在しないため録音できません。 programId={program.ProgramId}");
                throw new DomainException("聞き逃し配信URLが見つかりません。番組表更新後に再度お試しください。");
            }

            if (!program.OnDemandExpiresAtUtc.HasValue || program.OnDemandExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                logger.ZLogWarning($"聞き逃し配信の有効期限切れです。 programId={program.ProgramId} expiresUtc={program.OnDemandExpiresAtUtc}");
                throw new DomainException("聞き逃し配信の有効期限が切れています。");
            }

            streamUrl = program.OnDemandContentUrl;
        }
        else
        {
            var radiruAreaKind = Enum.GetValues<RadiruAreaKind>().First(r => r.GetEnumCodeId() == program.AreaId);
            var stationInformation = await stationLobLogic.GetNhkRadiruStationInformationByAreaAsync(radiruAreaKind);
            var radiruStationKind = Enumeration.GetAll<RadiruStationKind>().First(r => r.ServiceId == program.StationId);

            streamUrl = radiruStationKind switch
            {
                not null when radiruStationKind == RadiruStationKind.R1 => stationInformation.R1Hls,
                not null when radiruStationKind == RadiruStationKind.R2 => stationInformation.R2Hls,
                not null when radiruStationKind == RadiruStationKind.FM => stationInformation.FmHls,
                _ => throw new DomainException("放送局の判定ができませんでした。"),
            };
        }

        var programInfo = new ProgramRecordingInfo(
            ProgramId: program.ProgramId,
            Title: program.Title,
            Subtitle: program.Subtitle,
            StationId: program.StationId,
            StationName: program.StationName,
            AreaId: program.AreaId,
            StartTime: program.StartTime,
            EndTime: program.EndTime,
            Performer: program.Performer,
            Description: program.Description,
            ProgramUrl: program.ProgramUrl,
            ImageUrl: program.ImageUrl);

        var options = new RecordingOptions(
            ServiceKind: RadioServiceKind.Radiru,
            IsTimeFree: false,
            IsOnDemand: isOnDemand,
            StartDelaySeconds: command.StartDelaySeconds,
            EndDelaySeconds: command.EndDelaySeconds);

        return new RecordingSourceResult(
            StreamUrl: streamUrl,
            Headers: new Dictionary<string, string>(),
            ProgramInfo: programInfo,
            Options: options);
    }
}
