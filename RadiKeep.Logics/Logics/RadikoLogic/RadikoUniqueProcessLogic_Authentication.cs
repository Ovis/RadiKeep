using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using RadiKeep.Logics.Application;
using RadiKeep.Logics.Errors;
using RadiKeep.Logics.Models.Radiko;
using ZLogger;

namespace RadiKeep.Logics.Logics.RadikoLogic
{
    public partial class RadikoUniqueProcessLogic
    {
        public async ValueTask<(bool IsSuccess, string Session, bool IsPremiumUser, bool IsAreaFree)> TryLoginWithCredentialsAsync(
            string userId,
            string password)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                return (false, string.Empty, false, false);
            }

            using var response = await HttpClientExecutionHelper.SendWithRetryAsync(
                logger,
                HttpClient,
                "radiko login API",
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://radiko.jp/ap/member/webapi/member/login");
                    request.Headers.Add("Accept-Encoding", "gzip");
                    request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["mail"] = userId,
                        ["pass"] = password
                    });
                    return request;
                },
                config.ExternalServiceUserAgent);

            // 認証情報不正は 403 を返すため、保存前に失敗として扱う。
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (false, string.Empty, false, false);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.ZLogWarning($"radikoログインAPIが失敗しました。StatusCode={response.StatusCode}");
                return (false, string.Empty, false, false);
            }

            var json = await response.Content.ReadAsStringAsync();
            var loginResultEntity = JsonSerializer.Deserialize<RadikoLoginResult>(json);
            if (loginResultEntity == null || string.IsNullOrWhiteSpace(loginResultEntity.RadikoSession))
            {
                logger.ZLogWarning($"radikoログイン結果の解析に失敗しました。");
                return (false, string.Empty, false, false);
            }

            var isPremiumUser = loginResultEntity.PaidMember == "1";
            var isAreaFree = loginResultEntity.AreaFree == "1";
            return (true, loginResultEntity.RadikoSession, isPremiumUser, isAreaFree);
        }

        /// <summary>
        /// radikoにログイン
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, string Session, bool IsPremiumUser, bool IsAreaFree)> LoginRadikoAsync()
        {
            var (hasCredentials, userId, password) = await config.TryGetRadikoCredentialsAsync();
            if (!hasCredentials)
            {
                config.UpdateRadikoPremiumUser(false);
                config.UpdateRadikoAreaFree(false);
                return (false, string.Empty, false, false);
            }

            var (isSuccess, session, isPremiumUser, isAreaFree) = await TryLoginWithCredentialsAsync(userId, password);
            if (!isSuccess)
            {
                config.UpdateRadikoPremiumUser(false);
                config.UpdateRadikoAreaFree(false);
                return (false, string.Empty, false, false);
            }

            config.UpdateRadikoPremiumUser(isPremiumUser);
            config.UpdateRadikoAreaFree(isAreaFree);

            return (true, session, isPremiumUser, isAreaFree);
        }



        public async ValueTask<(bool IsSuccess, string Token, string AreaId)> AuthorizeRadikoAsync(string session = "")
        {
            if (string.IsNullOrEmpty(session))
                (var _, session, _, _) = await LoginRadikoAsync();

            string? token;
            string partialKey;
            {
                using var response = await HttpClientExecutionHelper.SendWithRetryAsync(
                    logger,
                    HttpClient,
                    "radiko auth1 API",
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, "https://radiko.jp/v2/api/auth1");
                        request.Headers.Add("pragma", "no-cache");
                        request.Headers.Add("x-radiko-app", "pc_html5");
                        request.Headers.Add("x-radiko-app-version", "0.0.1");
                        request.Headers.Add("x-radiko-device", "pc");
                        request.Headers.Add("x-radiko-user", "dummy_user");
                        return request;
                    },
                    config.ExternalServiceUserAgent);

                if (!response.IsSuccessStatusCode)
                {
                    return (false, string.Empty, string.Empty);
                }

                token = GetHeaderValue(response.Headers, "X-Radiko-AuthToken");
                int.TryParse(GetHeaderValue(response.Headers, "X-Radiko-KeyLength"), out var keyLength);
                int.TryParse(GetHeaderValue(response.Headers, "X-Radiko-KeyOffset"), out var keyOffset);

                if (string.IsNullOrEmpty(token))
                    return (false, string.Empty, string.Empty);

                var (isSuccess, key) = await GetPartialKeyString();

                if (!isSuccess)
                {
                    return (false, string.Empty, string.Empty);
                }

                partialKey =
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(key.Substring(keyOffset, keyLength)));
            }


            try
            {
                // トークンを有効化
                var queryString = HttpUtility.ParseQueryString("");
                queryString.Add("radiko_session", $"{session}");
                var uriBuilder = new UriBuilder("https://radiko.jp/v2/api/auth2") { Query = queryString.ToString() };

                using var response = await HttpClientExecutionHelper.SendWithRetryAsync(
                    logger,
                    HttpClient,
                    "radiko auth2 API",
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
                        request.Headers.Add("X-Radiko-AuthToken", token);
                        request.Headers.Add("X-Radiko-Device", "pc");
                        request.Headers.Add("X-Radiko-PartialKey", partialKey);
                        request.Headers.Add("x-radiko-user", "dummy_user");
                        return request;
                    },
                    config.ExternalServiceUserAgent);
                var body = (await response.Content.ReadAsStringAsync()).Replace("\r", "").Trim();

                if (string.IsNullOrWhiteSpace(body) || body == "OUT")
                {
                    return (false, string.Empty, string.Empty);
                }

                var areaId = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]
                    .Split(',')[0]
                    .Trim();

                if (string.IsNullOrWhiteSpace(areaId))
                {
                    return (false, string.Empty, string.Empty);
                }

                return (true, token!, areaId);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"radiko認証2でエラー");
                throw new DomainException("radiko認証処理に失敗しました。", e);
            }
        }

        /// <summary>
        /// radikoからログアウト
        /// </summary>
        /// <param name="session">radikoセッション</param>
        /// <returns>成功可否</returns>
        public async ValueTask<bool> LogoutRadikoAsync(string session)
        {
            if (string.IsNullOrWhiteSpace(session))
                return false;

            try
            {
                using var response = await HttpClientExecutionHelper.SendWithRetryAsync(
                    logger,
                    HttpClient,
                    "radiko logout API",
                    () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, "https://radiko.jp/v4/api/member/logout");
                        request.Headers.Add("Accept-Encoding", "gzip");
                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["radiko_session"] = session
                        });
                        return request;
                    },
                    config.ExternalServiceUserAgent);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"radikoログアウトで例外が発生しました。");
                return false;
            }
        }

        /// <summary>
        /// パーシャルキー生成に必要な文字列の取得
        /// </summary>
        /// <returns></returns>
        public async ValueTask<(bool IsSuccess, string Key)> GetPartialKeyString()
        {
            var key = string.Empty;
            try
            {
                // partial keyの元を取得
                using var response = await HttpClientExecutionHelper.SendWithRetryAsync(
                    logger,
                    HttpClient,
                    "radiko partial key API",
                    () => new HttpRequestMessage(HttpMethod.Get, "http://radiko.jp/apps/js/playerCommon.js"),
                    config.ExternalServiceUserAgent);
                var js = await response.Content.ReadAsStringAsync();

                var m = Regex.Match(js, @"new RadikoJSPlayer.*{");
                if (m.Success)
                {
                    key = m.Value.Split(",")[2].Replace("'", "").Trim();
                }
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"Failed to get partial key");
                return (false, string.Empty);
            }

            return (true, key);
        }

        private static string? GetHeaderValue(HttpResponseHeaders headers, string headerName)
        {
            if (headers.TryGetValues(headerName, out var values))
            {
                return values.FirstOrDefault();
            }

            return null;
        }
    }
}
