namespace RadiKeep.Areas.Api.Models;

/// <summary>
/// タグ作成・更新リクエスト
/// </summary>
public class TagUpsertRequest
{
    public string Name { get; set; } = string.Empty;
}
