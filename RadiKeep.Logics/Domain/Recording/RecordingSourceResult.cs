namespace RadiKeep.Logics.Domain.Recording;

/// <summary>
/// 録音ソースの解決結果
/// </summary>
/// <param name="StreamUrl">ストリームURL（タイムフリーの場合はplaylist_create_urlのベース）</param>
/// <param name="Headers">HTTPヘッダー</param>
/// <param name="ProgramInfo">番組情報</param>
/// <param name="Options">録音オプション</param>
public record RecordingSourceResult(
    string StreamUrl,
    IReadOnlyDictionary<string, string> Headers,
    ProgramRecordingInfo ProgramInfo,
    RecordingOptions Options);
