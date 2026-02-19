using RadiKeep.Logics.Domain.Recording;

namespace RadiKeep.Logics.Tests.Mocks;

/// <summary>
/// 録音実行のテスト用スタブ
/// </summary>
public class FakeMediaTranscodeService(bool result = true) : IMediaTranscodeService
{
    /// <summary>
    /// 固定の成否を返す
    /// </summary>
    public ValueTask<bool> RecordAsync(RecordingSourceResult source, MediaPath path, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(result);
    }
}
