using RadiKeep.Logics.Models;
using RadiKeep.Logics.Models.Enums;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Models.Radiko;
using RadiKeep.Logics.Primitives;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.RdbContext;
using RadiKeep.Logics.Services;
using RadiKeep.Logics.Extensions;

namespace RadiKeep.Logics.Mappers;

/// <summary>
/// DTO/Entity/Domain変換を集約するマッパー実装
/// </summary>
public class EntryMapper(IAppConfigurationService config) : IEntryMapper
{
    /// <summary>
    /// スケジュールジョブをスケジュールエントリに変換する
    /// </summary>
    public ScheduleEntry ToScheduleEntry(ScheduleJob source)
    {
        return new ScheduleEntry
        {
            Id = source.Id,
            ServiceKind = source.ServiceKind,
            StationId = source.StationId,
            StationName = config.ChooseStationName(source.ServiceKind, source.StationId),
            AreaId = source.AreaId,
            ProgramId = source.ProgramId,
            Title = source.Title,
            Subtitle = source.Subtitle,
            StartDateTime = source.StartDateTime,
            EndDateTime = source.EndDateTime,
            Performer = source.Performer,
            Description = source.Description,
            RecordingType = source.RecordingType,
            ReserveType = source.ReserveType,
            IsEnabled = source.IsEnabled
        };
    }

    /// <summary>
    /// radikoの放送局情報をエントリに変換する
    /// </summary>
    public RadikoStationInformationEntry ToRadikoStationInformationEntry(RadikoStation source)
    {
        return new RadikoStationInformationEntry
        {
            StationId = source.StationId,
            RegionId = source.RegionId,
            RegionName = source.RegionName,
            RegionOrder = source.RegionOrder,
            Area = source.Area,
            StationName = source.StationName,
            AreaFree = source.AreaFree,
            TimeFree = source.TimeFree,
            StationOrder = source.StationOrder
        };
    }

    /// <summary>
    /// らじる★らじるの放送局情報をエントリに変換する
    /// </summary>
    public RadiruStationEntry ToRadiruStationEntry(RadiruAreaKind areaKind, RadiruStationKind stationKind)
    {
        return new RadiruStationEntry
        {
            AreaId = areaKind.GetEnumCodeId(),
            AreaName = areaKind.ToString(),
            StationId = stationKind.ServiceId,
            StationName = stationKind.Name
        };
    }

    /// <summary>
    /// radikoの番組情報をエントリに変換する
    /// </summary>
    public RadioProgramEntry ToRadioProgramEntry(RadikoProgram source)
    {
        return new RadioProgramEntry
        {
            ProgramId = source.ProgramId,
            ServiceKind = RadioServiceKind.Radiko,
            AreaId = string.Empty,
            AreaName = string.Empty,
            StationId = source.StationId,
            StationName = config.RadikoStationDic[source.StationId],
            Title = source.Title,
            Subtitle = string.Empty,
            Performer = source.Performer,
            Description = source.Description.RemoveImageTagsFromHtml(),
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            AvailabilityTimeFree = source.AvailabilityTimeFree,
            ProgramUrl = source.ProgramUrl,
            ImageUrl = source.ImageUrl,
            OnDemandContentUrl = null,
            OnDemandExpiresAtUtc = null
        };
    }

    /// <summary>
    /// らじる★らじるの番組情報をエントリに変換する
    /// </summary>
    public RadioProgramEntry ToRadioProgramEntry(NhkRadiruProgram source)
    {
        return new RadioProgramEntry
        {
            ProgramId = source.ProgramId,
            ServiceKind = RadioServiceKind.Radiru,
            AreaId = source.AreaId,
            AreaName = Enum.GetValues<RadiruAreaKind>().Single(r => r.GetEnumCodeId() == source.AreaId).ToString(),
            StationId = source.StationId,
            StationName = Enumeration.GetAll<RadiruStationKind>().Single(r => r.ServiceId == source.StationId).Name,
            Title = source.Title,
            Subtitle = source.Subtitle,
            Performer = source.Performer,
            Description = source.Description.RemoveImageTagsFromHtml(),
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            AvailabilityTimeFree = AvailabilityTimeFree.Unavailable,
            ProgramUrl = source.ProgramUrl,
            ImageUrl = source.ImageUrl,
            OnDemandContentUrl = source.OnDemandContentUrl,
            OnDemandExpiresAtUtc = source.OnDemandExpiresAtUtc
        };
    }

    /// <summary>
    /// radikoの番組情報をAPI用エントリに変換する
    /// </summary>
    public ProgramForApiEntry ToRadikoProgramForApiEntry(RadikoProgram source)
    {
        return new ProgramForApiEntry
        {
            ProgramId = source.ProgramId,
            ServiceKind = RadioServiceKind.Radiko,
            AreaId = string.Empty,
            AreaName = string.Empty,
            StationId = source.StationId,
            StationName = config.RadikoStationDic[source.StationId],
            Title = source.Title,
            RadioDate = source.RadioDate,
            DaysOfWeek = source.DaysOfWeek,
            Performer = source.Performer,
            Description = source.Description.RemoveImageTagsFromHtml(),
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            ProgramUrl = source.ProgramUrl,
            AvailabilityTimeFree = source.AvailabilityTimeFree,
            OnDemandContentUrl = null,
            OnDemandExpiresAtUtc = null
        };
    }

    /// <summary>
    /// らじる★らじるの番組情報をAPI用エントリに変換する
    /// </summary>
    public ProgramForApiEntry ToRadiruProgramForApiEntry(NhkRadiruProgram source)
    {
        return new ProgramForApiEntry
        {
            ProgramId = source.ProgramId,
            ServiceKind = RadioServiceKind.Radiru,
            AreaId = source.AreaId,
            AreaName = Enum.GetValues<RadiruAreaKind>().Single(r => r.GetEnumCodeId() == source.AreaId).ToString(),
            StationId = source.StationId,
            StationName = Enumeration.GetAll<RadiruStationKind>().Single(r => r.ServiceId == source.StationId).Name,
            Title = source.Title,
            RadioDate = source.RadioDate,
            DaysOfWeek = source.DaysOfWeek,
            Performer = source.Performer,
            Description = source.Description.RemoveImageTagsFromHtml(),
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            ProgramUrl = source.ProgramUrl,
            AvailabilityTimeFree = AvailabilityTimeFree.Unavailable,
            OnDemandContentUrl = source.OnDemandContentUrl,
            OnDemandExpiresAtUtc = source.OnDemandExpiresAtUtc
        };
    }

    /// <summary>
    /// キーワード予約の永続化モデルをエントリに変換する
    /// </summary>
    public KeywordReserveEntry ToKeywordReserveEntry(KeywordReserve source, List<Guid>? tagIds = null, List<string>? tagNames = null)
    {
        return new KeywordReserveEntry
        {
            Id = source.Id,
            Keyword = source.Keyword,
            ExcludedKeyword = source.ExcludedKeyword,
            RecordPath = source.FolderPath,
            RecordFileName = source.FileName,
            SelectedDaysOfWeek = source.DaysOfWeek.ToList(),
            SearchTitleOnly = source.IsTitleOnly,
            ExcludeTitleOnly = source.IsExcludeTitleOnly,
            StartTimeString = source.StartTime.ToString("HH:mm"),
            EndTimeString = source.EndTime.ToString("HH:mm"),
            IsEnabled = source.IsEnable,
            StartDelay = source.StartDelay.HasValue ? source.StartDelay.Value.TotalSeconds : default(double?),
            EndDelay = source.EndDelay.HasValue ? source.EndDelay.Value.TotalSeconds : default(double?),
            SortOrder = source.SortOrder,
            MergeTagBehavior = source.MergeTagBehavior,
            TagIds = tagIds ?? [],
            Tags = tagNames ?? []
        };
    }

    /// <summary>
    /// 通知の永続化モデルをエントリに変換する
    /// </summary>
    public NotificationEntry ToNotificationEntry(Notification source)
    {
        return new NotificationEntry
        {
            LogLevel = source.LogLevel,
            Category = source.Category,
            Message = source.Message,
            Timestamp = source.Timestamp,
            IsRead = source.IsRead
        };
    }

    /// <summary>
    /// 通知エントリを永続化モデルに変換する
    /// </summary>
    public Notification ToNotification(NotificationEntry source)
    {
        return new Notification
        {
            Id = Ulid.NewUlid(),
            LogLevel = source.LogLevel,
            Category = source.Category,
            Message = source.Message,
            Timestamp = source.Timestamp.ToUniversalTime(),
            IsRead = source.IsRead
        };
    }

    /// <summary>
    /// 録音済み番組の一覧エントリに変換する
    /// </summary>
    public RecordedProgramEntry ToRecordedProgramEntry(Recording recording, RecordingMetadata metadata, RecordingFile file, List<string>? tags = null)
    {
        return new RecordedProgramEntry
        {
            Id = recording.Id,
            Title = metadata.Title,
            StationName = ResolveStationName(recording.ServiceKind, recording.StationId, metadata.StationName),
            StartDateTime = recording.StartDateTime,
            EndDateTime = recording.EndDateTime,
            Duration = (recording.EndDateTime - recording.StartDateTime).TotalSeconds,
            FilePath = file.FileRelativePath,
            Tags = tags ?? [],
            IsListened = recording.IsListened
        };
    }

    /// <summary>
    /// 放送局名を解決する
    /// </summary>
    private string ResolveStationName(RadioServiceKind kind, string stationId, string fallbackStationName)
    {
        return kind switch
        {
            RadioServiceKind.Radiko => config.RadikoStationDic.TryGetValue(stationId, out var stationName) ? stationName : fallbackStationName,
            RadioServiceKind.Radiru => Enumeration.GetAll<RadiruStationKind>().Single(r => r.ServiceId == stationId).Name,
            _ => string.IsNullOrWhiteSpace(fallbackStationName) ? "不明" : fallbackStationName
        };
    }
}
