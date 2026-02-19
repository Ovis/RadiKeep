using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Interfaces;

/// <summary>
/// radikoのAPI通信を行うインターフェース
/// </summary>
public interface IRadikoApiClient
{
    /// <summary>
    /// radikoの放送局一覧を取得
    /// </summary>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns></returns>
    Task<List<RadikoStation>> GetRadikoStationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定されたエリアに対応する放送局IDのリストを取得
    /// </summary>
    /// <param name="area">エリアコード</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>放送局IDのリスト</returns>
    Task<List<string>> GetStationsByAreaAsync(string area, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定の放送局の週間番組表を取得
    /// </summary>
    /// <param name="stationId">放送局ID</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>番組リスト</returns>
    Task<List<RadikoProgram>> GetWeeklyProgramsAsync(string stationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// タイムフリー録音用のplaylist_create_urlを取得する
    /// </summary>
    /// <param name="stationId">放送局ID</param>
    /// <param name="isAreaFree">areafreeの有無</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>playlist_create_urlのリスト</returns>
    Task<List<string>> GetTimeFreePlaylistCreateUrlsAsync(string stationId, bool isAreaFree, CancellationToken cancellationToken = default);
}
