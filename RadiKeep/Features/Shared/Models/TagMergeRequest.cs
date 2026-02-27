namespace RadiKeep.Features.Shared.Models;

/// <summary>
/// タグ統合リクエスト
/// </summary>
public class TagMergeRequest
{
    public Guid FromTagId { get; set; }
    public Guid ToTagId { get; set; }
}

