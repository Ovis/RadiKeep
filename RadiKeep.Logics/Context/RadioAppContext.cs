using System.Globalization;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Primitives;

namespace RadiKeep.Logics.Context;

public class RadioAppContext(DateTimeOffset? standardDateTimeOffset = null) : IRadioAppContext
{
    public DateTimeOffset StandardDateTimeOffset { get; } = standardDateTimeOffset ?? DateTimeOffset.UtcNow.ToNormalizedByTz();

    public TimeZoneInfo TimeZoneInfo { get; } = JapanTimeZone.Resolve();

    public CultureInfo CultureInfo { get; } = new("ja-JP");
}
