using RadiKeep.Logics.Primitives;

namespace RadiKeep.Application
{
    public class OverrideJapanTimeProvider : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone =>
            JapanTimeZone.Resolve();
    }
}
