using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Models.NhkRadiru.JsonEntity;

namespace RadiKeep.Logics.Interfaces;

public interface IRadiruApiClient
{
    /// <summary>
    /// 取得対象のエリアID/サービスID組一覧を取得する
    /// </summary>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>エリアID/サービスID組一覧</returns>
    ValueTask<List<(string AreaId, string ServiceId)>> GetAvailableAreaServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定日の番組表を取得する
    /// </summary>
    /// <param name="area">エリア</param>
    /// <param name="stationKind">放送局</param>
    /// <param name="date">取得する日付</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>番組リスト</returns>
    Task<List<RadiruProgramJsonEntity>> GetDailyProgramsAsync(
        RadiruAreaKind area,
        RadiruStationKind stationKind,
        DateTimeOffset date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定日の番組表を取得する
    /// </summary>
    /// <param name="areaId">エリアID</param>
    /// <param name="serviceId">サービスID</param>
    /// <param name="date">取得する日付</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>番組リスト</returns>
    Task<List<RadiruProgramJsonEntity>> GetDailyProgramsAsync(
        string areaId,
        string serviceId,
        DateTimeOffset date,
        CancellationToken cancellationToken = default);
}
