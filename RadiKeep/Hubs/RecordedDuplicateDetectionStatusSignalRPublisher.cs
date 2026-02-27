using Microsoft.AspNetCore.SignalR;
using RadiKeep.Logics.Logics.RecordedRadioLogic;
using RadiKeep.Logics.Models;
using ZLogger;

namespace RadiKeep.Hubs;

/// <summary>
/// 同一番組候補チェック状態を SignalR で配信する実装。
/// </summary>
public class RecordedDuplicateDetectionStatusSignalRPublisher(
    ILogger<RecordedDuplicateDetectionStatusSignalRPublisher> logger,
    IHubContext<RecordedDuplicateDetectionHub> hubContext) : IRecordedDuplicateDetectionStatusPublisher
{
    /// <summary>
    /// 同一番組候補チェック状態を全クライアントに配信する。
    /// </summary>
    /// <param name="status">現在状態</param>
    /// <param name="cancellationToken">キャンセル用トークン</param>
    public async ValueTask PublishAsync(RecordedDuplicateDetectionStatusEntry status, CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(RecordedDuplicateDetectionHubMethods.StatusChanged, status, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"同一番組候補チェック状態イベントの配信に失敗しました。");
        }
    }
}
