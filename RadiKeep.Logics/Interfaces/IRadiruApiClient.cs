using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Models.NhkRadiru.JsonEntity;

namespace RadiKeep.Logics.Interfaces;

public interface IRadiruApiClient
{
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
}
