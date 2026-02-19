using System.Globalization;
using RadiKeep.Logics.Context;

namespace RadiKeep.Logics.Tests.Mocks;

/// <summary>
/// アプリケーションコンテキストのテスト用スタブ
/// </summary>
public class FakeRadioAppContext : IRadioAppContext
{
    public DateTimeOffset StandardDateTimeOffset { get; set; } = DateTimeOffset.UtcNow;

    public TimeZoneInfo TimeZoneInfo { get; set; } = TimeZoneInfo.Utc;

    public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;
}
