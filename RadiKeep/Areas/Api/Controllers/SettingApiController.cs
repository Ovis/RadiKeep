using Microsoft.AspNetCore.Mvc;
using RadiKeep.Areas.Api.Models;
using RadiKeep.Logics.Extensions;
using RadiKeep.Logics.Logics.ProgramScheduleLogic;
using RadiKeep.Logics.Logics.RadikoLogic;
using RadiKeep.Logics.Logics.RecordedRadioLogic;
using RadiKeep.Logics.Logics.ReserveLogic;
using RadiKeep.Logics.Models.NhkRadiru;
using RadiKeep.Logics.Primitives.DataAnnotations;
using RadiKeep.Logics.Services;
using ZLogger;

namespace RadiKeep.Areas.Api.Controllers
{
    [Area("api")]
    [ApiController]
    [Route("/api/v1/settings")]
    public class SettingApiController(
        ILogger<SettingApiController> logger,
        IAppConfigurationService appConfigurationService,
        ReserveLobLogic reserveLobLogic,
        ProgramScheduleLobLogic programScheduleLobLogic,
        RadikoUniqueProcessLogic radikoUniqueProcessLogic,
        RecordedDuplicateDetectionLobLogic duplicateDetectionLobLogic) : ControllerBase
    {
        /// <summary>
        /// 番組の保存先フォルダパス設定
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("record-directory")]
        public async ValueTask<IActionResult> UpdateRecordDirectoryPathAsync(UpdateRecordDirectoryPathEntity entity)
        {
            // 空文字は「保存先ルート直下を使う」設定として許容する
            if (!string.IsNullOrWhiteSpace(entity.DirectoryPath) && !entity.DirectoryPath.IsValidRelativePath())
            {
                return BadRequest(ApiResponse.Fail("保存先フォルダパスが不正です。"));
            }

            await appConfigurationService.UpdateRecordDirectoryPathAsync(entity.DirectoryPath);

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 番組録音時のファイル名設定
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("record-filename")]
        public async ValueTask<IActionResult> UpdateRecordFileNameTemplateAsync(UpdateRecordFileNameTemplateEntity entity)
        {
            // 空文字は既定ファイル名へフォールバックする設定として許容する
            if (!string.IsNullOrWhiteSpace(entity.FileNameTemplate) && !entity.FileNameTemplate.IsValidFileName())
            {
                return BadRequest(ApiResponse.Fail("ファイル名テンプレートが不正です。"));
            }

            await appConfigurationService.UpdateRecordFileNameTemplateAsync(entity.FileNameTemplate);

            return Ok(ApiResponse.Ok("更新しました。"));
        }


        /// <summary>
        /// 録音時間のマージン設定
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("duration")]
        public async ValueTask<IActionResult> UpdateDurationAsync(UpdateDurationEntity entity)
        {
            try
            {
                await appConfigurationService.UpdateDurationAsync(entity.StartDuration ?? 0, entity.EndDuration ?? 0);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"録音時間のマージン設定でエラーが発生しました。");
                return BadRequest(ApiResponse.Fail("録音時間のマージン設定に失敗しました。"));
            }

            try
            {
                await reserveLobLogic.UpdateReserveDurationAsync();
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"録音時間のマージン設定でエラーが発生しました。");
                return BadRequest(ApiResponse.Fail("録音時間のマージン設定に失敗しました。"));
            }

            return Ok(ApiResponse.Ok("更新しました。"));
        }


        /// <summary>
        /// らじる★らじるのエリア設定
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("radiru-area")]
        public async ValueTask<IActionResult> UpdateRadiruAreaAsync(UpdateRadiruAreaEntity entity)
        {

            if (Enum.GetValues<RadiruAreaKind>().All(r => r.GetEnumCodeId() != entity.RadiruArea))
            {
                logger.ZLogError($"指定されたエリア情報が不正です。");
                return BadRequest(ApiResponse.Fail("指定されたエリア情報が不正です。"));
            }

            try
            {
                await appConfigurationService.UpdateRadiruAreaAsync(entity.RadiruArea);
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"らじる\u2605らじるのエリア設定でエラーが発生しました。");
                return BadRequest(ApiResponse.Fail("らじる★らじるのエリア設定に失敗しました。"));
            }

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 録音時間のマージン設定
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("notice")]
        public async ValueTask<IActionResult> UpdateNotificationSettingAsync(UpdateNotificationSettingEntity entity)
        {
            await appConfigurationService.UpdateNoticeSettingAsync(
                discordWebhookUrl: entity.DiscordWebhookUrl,
                selectedNoticeCategories: entity.NotificationCategories
                );

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 未読バッジ件数に含めるお知らせカテゴリ設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("unread-badge-notice-categories")]
        public async ValueTask<IActionResult> UpdateUnreadBadgeNoticeCategoriesAsync(UpdateUnreadBadgeNoticeCategoriesEntity entity)
        {
            await appConfigurationService.UpdateUnreadBadgeNoticeCategoriesAsync(entity.NotificationCategories);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 外部サービス接続時のUser-Agent設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("external-service-user-agent")]
        public async ValueTask<IActionResult> UpdateExternalServiceUserAgentAsync(UpdateExternalServiceUserAgentEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.UserAgent))
            {
                return BadRequest(ApiResponse.Fail("User-Agent を入力してください。"));
            }

            if (entity.UserAgent.Length > 1024)
            {
                return BadRequest(ApiResponse.Fail("User-Agent が長すぎます。1024文字以内で入力してください。"));
            }

            try
            {
                await appConfigurationService.UpdateExternalServiceUserAgentAsync(entity.UserAgent.Trim());
            }
            catch (Exception e)
            {
                logger.ZLogError(e, $"外部サービス接続時の User-Agent 設定でエラーが発生しました。");
                return BadRequest(ApiResponse.Fail("接続設定（User-Agent）の保存に失敗しました。"));
            }

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// らじる★らじるAPIアクセス間隔設定（詳細設定）
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("external-service-radiru-request")]
        public async ValueTask<IActionResult> UpdateRadiruRequestSettingsAsync(UpdateRadiruRequestSettingsEntity entity)
        {
            if (entity.MinRequestIntervalMs < 0 || entity.MinRequestIntervalMs > 60000)
            {
                return BadRequest(ApiResponse.Fail("最小待機時間は 0〜60000 ミリ秒の範囲で指定してください。"));
            }
            if (entity.RequestJitterMs < 0 || entity.RequestJitterMs > 60000)
            {
                return BadRequest(ApiResponse.Fail("ランダム揺らぎは 0〜60000 ミリ秒の範囲で指定してください。"));
            }

            await appConfigurationService.UpdateRadiruApiRequestSettingsAsync(
                entity.MinRequestIntervalMs,
                entity.RequestJitterMs);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// radikoログイン情報を更新
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("radiko-login")]
        public async ValueTask<IActionResult> UpdateRadikoLoginAsync(UpdateRadikoLoginEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.UserId) || string.IsNullOrWhiteSpace(entity.Password))
            {
                return BadRequest(ApiResponse.Fail("ユーザーIDまたはパスワードが不正です。"));
            }

            var userId = entity.UserId.Trim();
            var password = entity.Password;

            var (isLoginSuccess, _, isPremiumUser, isAreaFree) = await radikoUniqueProcessLogic.TryLoginWithCredentialsAsync(
                userId,
                password);
            if (!isLoginSuccess)
            {
                return BadRequest(ApiResponse.Fail("radikoログインに失敗しました。メールアドレスまたはパスワードを確認してください。"));
            }

            await appConfigurationService.UpdateRadikoCredentialsAsync(userId, password);
            appConfigurationService.UpdateRadikoPremiumUser(isPremiumUser);
            appConfigurationService.UpdateRadikoAreaFree(isAreaFree);

            if (isAreaFree)
            {
                var hasEnoughProgramData = await programScheduleLobLogic.HasRadikoProgramsForAllStationsThroughAsync();
                if (hasEnoughProgramData)
                {
                    return Ok(ApiResponse.Ok("更新しました。エリアフリー会員の番組表データは最新範囲まで取得済みのため、更新処理はスキップしました。"));
                }

                var (isScheduled, scheduleError) = await programScheduleLobLogic.ScheduleImmediateUpdateProgramJobAsync();
                if (!isScheduled)
                {
                    logger.ZLogError(scheduleError, $"radikoログイン成功後の番組表即時更新ジョブ登録に失敗しました。");
                    return BadRequest(ApiResponse.Fail("radikoログイン情報は保存しましたが、自動予約反映のための更新処理開始に失敗しました。番組表更新を手動実行してください。"));
                }

                return Ok(ApiResponse.Ok("更新しました。エリアフリー会員のため、自動予約反映の番組表更新処理を開始しました。"));
            }

            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// radikoログイン情報を削除
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("radiko-logout")]
        public async ValueTask<IActionResult> ClearRadikoLoginAsync()
        {
            try
            {
                var (hasCredentials, _, _) = await appConfigurationService.TryGetRadikoCredentialsAsync();
                if (hasCredentials)
                {
                    var (isSuccess, session, _, _) = await radikoUniqueProcessLogic.LoginRadikoAsync();
                    if (isSuccess)
                    {
                        await radikoUniqueProcessLogic.LogoutRadikoAsync(session);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ZLogWarning(ex, $"radikoログアウトの呼び出しに失敗しました。");
            }

            await appConfigurationService.ClearRadikoCredentialsAsync();
            return Ok(ApiResponse.Ok("削除しました。"));
        }

        /// <summary>
        /// radikoエリア情報を強制再判定する
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("radiko-area/refresh")]
        public async ValueTask<IActionResult> RefreshRadikoAreaAsync()
        {
            var (isSuccess, area) = await radikoUniqueProcessLogic.RefreshRadikoAreaCacheAsync();
            if (!isSuccess || string.IsNullOrWhiteSpace(area))
            {
                return BadRequest(ApiResponse.Fail("radikoエリア情報の再判定に失敗しました。"));
            }

            return Ok(ApiResponse.Ok($"radikoエリア情報を再判定しました。（{area}）"));
        }

        /// <summary>
        /// 外部取込時のファイル更新日時タイムゾーン設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("external-import-timezone")]
        public async ValueTask<IActionResult> UpdateExternalImportTimeZoneAsync(UpdateExternalImportTimeZoneEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.TimeZoneId))
            {
                return BadRequest(ApiResponse.Fail("タイムゾーンIDが未入力です。"));
            }

            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(entity.TimeZoneId);
            }
            catch
            {
                return BadRequest(ApiResponse.Fail("指定されたタイムゾーンIDは無効です。"));
            }

            await appConfigurationService.UpdateExternalImportFileTimeZoneAsync(entity.TimeZoneId);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 保存先ストレージ空き容量不足通知しきい値（MB）設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("storage-low-space-threshold")]
        public async ValueTask<IActionResult> UpdateStorageLowSpaceThresholdAsync(UpdateStorageLowSpaceThresholdEntity entity)
        {
            if (entity.ThresholdMb <= 0 || entity.ThresholdMb > int.MaxValue)
            {
                return BadRequest(ApiResponse.Fail($"しきい値は1以上 {int.MaxValue} 以下の数値（MB）で指定してください。"));
            }

            await appConfigurationService.UpdateStorageLowSpaceThresholdMbAsync((int)entity.ThresholdMb);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 監視関連設定（詳細設定）
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("monitoring-advanced")]
        public async ValueTask<IActionResult> UpdateMonitoringAdvancedAsync(UpdateMonitoringAdvancedEntity entity)
        {
            if (entity.LogRetentionDays <= 0 || entity.LogRetentionDays > 3650)
            {
                return BadRequest(ApiResponse.Fail("ログ保持日数は 1〜3650 日の範囲で指定してください。"));
            }
            if (entity.StorageLowSpaceCheckIntervalMinutes <= 0 || entity.StorageLowSpaceCheckIntervalMinutes > 1440)
            {
                return BadRequest(ApiResponse.Fail("空き容量チェック間隔は 1〜1440 分の範囲で指定してください。"));
            }
            if (entity.StorageLowSpaceNotificationCooldownHours <= 0 || entity.StorageLowSpaceNotificationCooldownHours > 168)
            {
                return BadRequest(ApiResponse.Fail("通知クールダウンは 1〜168 時間の範囲で指定してください。"));
            }

            await appConfigurationService.UpdateMonitoringSettingsAsync(
                entity.LogRetentionDays,
                entity.StorageLowSpaceCheckIntervalMinutes,
                entity.StorageLowSpaceNotificationCooldownHours);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 複数キーワード一致時のタグ集約付与設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("merge-tags-from-matched-rules")]
        public async ValueTask<IActionResult> UpdateMergeTagsFromMatchedRulesAsync(UpdateMergeTagsFromMatchedRulesEntity entity)
        {
            await appConfigurationService.UpdateMergeTagsFromAllMatchedKeywordRulesAsync(entity.Enabled);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 録音時の番組イメージ埋め込み設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("embed-program-image-on-record")]
        public async ValueTask<IActionResult> UpdateEmbedProgramImageOnRecordAsync(UpdateEmbedProgramImageOnRecordEntity entity)
        {
            await appConfigurationService.UpdateEmbedProgramImageOnRecordAsync(entity.Enabled);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// ページ遷移時の再生復帰設定
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("resume-playback-across-pages")]
        public async ValueTask<IActionResult> UpdateResumePlaybackAcrossPagesAsync(UpdateResumePlaybackAcrossPagesEntity entity)
        {
            await appConfigurationService.UpdateResumePlaybackAcrossPagesAsync(entity.Enabled);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 新しいリリース確認設定（無効/1日/7日/30日）
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("release-check-interval")]
        public async ValueTask<IActionResult> UpdateReleaseCheckIntervalAsync(UpdateReleaseCheckIntervalEntity entity)
        {
            var allowedIntervals = new[] { 0, 1, 7, 30 };
            if (!allowedIntervals.Contains(entity.IntervalDays))
            {
                return BadRequest(ApiResponse.Fail("チェック間隔は 0, 1, 7, 30 のいずれかで指定してください。"));
            }

            await appConfigurationService.UpdateReleaseCheckIntervalDaysAsync(entity.IntervalDays);
            return Ok(ApiResponse.Ok("更新しました。"));
        }

        /// <summary>
        /// 類似録音抽出の定期実行設定（有効/無効、曜日、時刻）
        /// </summary>
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Route("duplicate-detection-interval")]
        public async ValueTask<IActionResult> UpdateDuplicateDetectionIntervalAsync(UpdateDuplicateDetectionIntervalEntity entity)
        {
            if (entity.DayOfWeek is < 0 or > 6)
            {
                return BadRequest(ApiResponse.Fail("曜日は 0(日曜) 〜 6(土曜) の範囲で指定してください。"));
            }
            if (entity.Hour is < 0 or > 23)
            {
                return BadRequest(ApiResponse.Fail("時は 0〜23 の範囲で指定してください。"));
            }
            if (entity.Minute is < 0 or > 59)
            {
                return BadRequest(ApiResponse.Fail("分は 0〜59 の範囲で指定してください。"));
            }

            await appConfigurationService.UpdateDuplicateDetectionScheduleAsync(
                entity.Enabled,
                entity.DayOfWeek,
                entity.Hour,
                entity.Minute);
            await duplicateDetectionLobLogic.SchedulePeriodicJobAsync();

            return Ok(ApiResponse.Ok("更新しました。"));
        }
    }


    public class UpdateRecordDirectoryPathEntity
    {
        public string DirectoryPath { get; set; } = string.Empty;
    }

    public class UpdateRecordFileNameTemplateEntity
    {
        public string FileNameTemplate { get; set; } = string.Empty;
    }

    public class UpdateDurationEntity
    {
        public int? StartDuration { get; set; }

        public int? EndDuration { get; set; }
    }

    public class UpdateRadiruAreaEntity
    {
        public string RadiruArea { get; set; } = string.Empty;
    }

    public class UpdateExternalServiceUserAgentEntity
    {
        public string UserAgent { get; set; } = string.Empty;
    }

    public class UpdateNotificationSettingEntity
    {
        public string DiscordWebhookUrl { get; set; } = string.Empty;
        public List<int> NotificationCategories { get; set; } = new();
    }

    public class UpdateUnreadBadgeNoticeCategoriesEntity
    {
        public List<int> NotificationCategories { get; set; } = new();
    }

    public class UpdateExternalImportTimeZoneEntity
    {
        public string TimeZoneId { get; set; } = string.Empty;
    }

    public class UpdateStorageLowSpaceThresholdEntity
    {
        public long ThresholdMb { get; set; }
    }

    public class UpdateMergeTagsFromMatchedRulesEntity
    {
        public bool Enabled { get; set; }
    }

    public class UpdateEmbedProgramImageOnRecordEntity
    {
        public bool Enabled { get; set; }
    }

    public class UpdateRadiruRequestSettingsEntity
    {
        public int MinRequestIntervalMs { get; set; }
        public int RequestJitterMs { get; set; }
    }

    public class UpdateResumePlaybackAcrossPagesEntity
    {
        public bool Enabled { get; set; }
    }

    public class UpdateMonitoringAdvancedEntity
    {
        public int LogRetentionDays { get; set; }
        public int StorageLowSpaceCheckIntervalMinutes { get; set; }
        public int StorageLowSpaceNotificationCooldownHours { get; set; }
    }

    public class UpdateReleaseCheckIntervalEntity
    {
        public int IntervalDays { get; set; }
    }

    public class UpdateDuplicateDetectionIntervalEntity
    {
        public bool Enabled { get; set; }
        public int DayOfWeek { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
    }
}
