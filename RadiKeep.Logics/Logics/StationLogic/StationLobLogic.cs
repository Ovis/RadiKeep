using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Domain.Station;
using RadiKeep.Logics.Interfaces;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Mappers;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Logics.StationLogic
{
    public partial class StationLobLogic(
        ILogger<StationLobLogic> logger,
        IAppConfigurationService config,
        IRadikoApiClient radikoApiClient,
        IStationRepository stationRepository,
        RadikoUniqueProcessLogic radikoUniqueProcessLogic,
        IHttpClientFactory httpClientFactory,
        IEntryMapper entryMapper)
    {
        private static readonly IMemoryCache Cache =
            new MemoryCache(new MemoryCacheOptions
            {
                ExpirationScanFrequency = TimeSpan.FromSeconds(60),
            });

        private static readonly TimeSpan AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
    }
}

