using RadiKeep.Logics.RdbContext;

namespace RadiKeep.Logics.Domain.Station;

/// <summary>
/// 放送局情報の永続化を担うリポジトリ
/// </summary>
public interface IStationRepository
{
    /// <summary>
    /// radiko放送局が初期化済みか確認する
    /// </summary>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>初期化済みならtrue</returns>
    ValueTask<bool> HasAnyRadikoStationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// radiko放送局を取得する
    /// </summary>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>放送局一覧</returns>
    ValueTask<List<RadikoStation>> GetRadikoStationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// radiko放送局を追加する
    /// </summary>
    /// <param name="stations">放送局一覧</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    ValueTask AddRadikoStationsIfMissingAsync(IEnumerable<RadikoStation> stations, CancellationToken cancellationToken = default);

    /// <summary>
    /// らじる★らじる放送局が初期化済みか確認する
    /// </summary>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>初期化済みならtrue</returns>
    ValueTask<bool> HasAnyRadiruStationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// らじる★らじる放送局を追加または更新する
    /// </summary>
    /// <param name="stations">放送局一覧</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    ValueTask UpsertRadiruStationsAsync(IEnumerable<NhkRadiruStation> stations, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定エリアのらじる★らじる放送局情報を取得する
    /// </summary>
    /// <param name="areaId">エリアID</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    /// <returns>放送局情報</returns>
    ValueTask<NhkRadiruStation> GetRadiruStationByAreaAsync(string areaId, CancellationToken cancellationToken = default);
}
