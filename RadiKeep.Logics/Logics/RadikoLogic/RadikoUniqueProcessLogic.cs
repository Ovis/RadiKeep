using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RadiKeep.Logics.Application;
using RadiKeep.Logics.Services;

namespace RadiKeep.Logics.Logics.RadikoLogic
{
    /// <summary>
    /// radikoの固有処理用ロジック
    /// </summary>
    public partial class RadikoUniqueProcessLogic(
        ILogger<RadikoUniqueProcessLogic> logger,
        IAppConfigurationService config,
        IHttpClientFactory httpClientFactory)
    {
        private sealed record CachedRadikoSession(
            int ScopeId,
            string Session,
            bool IsPremiumUser,
            bool IsAreaFree,
            DateTimeOffset ExpiresAtUtc);

        private sealed record CachedRadikoAuthorization(
            int ScopeId,
            string Session,
            string Token,
            string AreaId,
            DateTimeOffset ExpiresAtUtc);

        private const string RadikoAreaCacheKey = "radiko_current_area";
        private static readonly TimeSpan RadikoSessionCacheTtl = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan RadikoAuthorizationCacheTtl = TimeSpan.FromMinutes(1);
        private static readonly IMemoryCache AreaCache = new MemoryCache(
            new MemoryCacheOptions
            {
                ExpirationScanFrequency = TimeSpan.FromMinutes(1)
            });
        private static readonly SemaphoreSlim AuthenticationCacheLock = new(1, 1);
        private static readonly TimeSpan AreaCacheTtl = TimeSpan.FromHours(24);
        private static CachedRadikoSession? _cachedSession;
        private static CachedRadikoAuthorization? _cachedAuthorization;

        private readonly int _authenticationCacheScopeId = RuntimeHelpers.GetHashCode(httpClientFactory);
        private HttpClient HttpClient => httpClientFactory.CreateClient(HttpClientNames.Radiko);

        /// <summary>
        /// radiko の認証キャッシュを破棄する
        /// </summary>
        public void InvalidateAuthenticationCache()
        {
            AuthenticationCacheLock.Wait();
            try
            {
                _cachedSession = null;
                _cachedAuthorization = null;
            }
            finally
            {
                AuthenticationCacheLock.Release();
            }
        }

        /// <summary>
        /// radikoのエリアを取得する
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, string Area)> GetRadikoAreaAsync(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                AreaCache.Remove(RadikoAreaCacheKey);
            }

            if (!forceRefresh && AreaCache.TryGetValue<string>(RadikoAreaCacheKey, out var cachedArea) && !string.IsNullOrWhiteSpace(cachedArea))
            {
                return (true, cachedArea);
            }

            var result = await FetchRadikoAreaAsync();
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Area))
            {
                AreaCache.Set(RadikoAreaCacheKey, result.Area, AreaCacheTtl);
            }

            return result;
        }

        /// <summary>
        /// radikoエリアのキャッシュを破棄して再取得する
        /// </summary>
        public ValueTask<(bool IsSuccess, string Area)> RefreshRadikoAreaCacheAsync()
        {
            return GetRadikoAreaAsync(forceRefresh: true);
        }

        private async ValueTask<(bool IsSuccess, string Area)> FetchRadikoAreaAsync()
        {
            using var response = await HttpClientExecutionHelper.SendWithRetryAsync(
                logger,
                HttpClient,
                "radiko area API",
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "http://radiko.jp/area/");
                    request.Headers.Add("Accept-Encoding", "gzip");
                    return request;
                },
                config.ExternalServiceUserAgent);

            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Empty);
            }

            var text = await response.Content.ReadAsStringAsync();
            var m = Regex.Match(text, @"JP[0-9]+");

            return m.Success ? (true, m.Value) : (false, string.Empty);
        }
    }
}
