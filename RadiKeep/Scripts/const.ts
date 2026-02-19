export const API_ENDPOINTS = {
    RADIO_DATE: `/api/v1/general/radio-dates`,
    STATION_LIST_RADIKO: `/api/v1/programs/stations/radiko`,
    STATION_LIST_RADIRU: `/api/v1/programs/stations/radiru`,
    PROGRAM_DETAIL: `/api/v1/programs/detail`,
    PROGRAM_SEARCH: `/api/v1/programs/search`,
    PROGRAM_NOW: `/api/v1/programs/now`,
    PROGRAM_RECORDED: `/api/v1/recordings`,
    PROGRAM_RECORDED_STATIONS: `/api/v1/recordings/stations`,
    PROGRAM_RECORDED_DUPLICATES_RUN: `/api/v1/recordings/duplicates/run`,
    PROGRAM_RECORDED_DUPLICATES_STATUS: `/api/v1/recordings/duplicates/status`,
    PROGRAM_RECORDED_DUPLICATES_CANDIDATES: `/api/v1/recordings/duplicates/candidates`,
    TAGS: `/api/v1/tags`,
    RECORDING_TAGS_BULK_ADD: `/api/v1/recordings/tags/bulk-add`,
    RECORDING_TAGS_BULK_REMOVE: `/api/v1/recordings/tags/bulk-remove`,
    RECORDING_LISTENED_BULK_UPDATE: `/api/v1/recordings/listened/bulk`,
    PROGRAM_RESERVE: `/api/v1/programs/reserve`,
    PROGRAM_LIST_RADIKO: `/api/v1/programs/list/radiko`,
    PROGRAM_LIST_RADIRU: `/api/v1/programs/list/radiru`,
    PROGRAM_PLAY: `/api/v1/programs/play`,
    KEYWORD_RESERVE: `/api/v1/programs/keyword-reserve`,
    RESERVE_PROGRAM_LIST: `/api/v1/reserves/programs`,
    RESERVE_KEYWORD_LIST: `/api/v1/reserves/keywords`,
    RESERVE_PROGRAM_DELETE: `/api/v1/reserves/programs/delete`,
    RESERVE_SWITCH_STATUS: `/api/v1/reserves/keywords/switch`,
    RESERVE_KEYWORD_REORDER: `/api/v1/reserves/keywords/reorder`,
    UPDATE_KEYWORD_RESERVE: `/api/v1/reserves/keywords/update`,
    DELETE_KEYWORD_RESERVE: `/api/v1/reserves/keywords/delete`,
    DELETE_PROGRAM: `/api/v1/recordings/delete`,
    DELETE_PROGRAM_BULK: `/api/v1/recordings/delete/bulk`,
    DOWNLOAD_PROGRAM: `/api/v1/recordings/download/`,
    SETTING_RECORD_DIC_PATH: `/api/v1/settings/record-directory`,
    SETTING_RECORD_FILENAME_TEMPLATE: `/api/v1/settings/record-filename`,
    SETTING_DURATION: `/api/v1/settings/duration`,
    SETTING_RADIRU_AREA: `/api/v1/settings/radiru-area`,
    SETTING_EXTERNAL_SERVICE_USER_AGENT: `/api/v1/settings/external-service-user-agent`,
    SETTING_EXTERNAL_SERVICE_RADIRU_REQUEST: `/api/v1/settings/external-service-radiru-request`,
    SETTING_NOTICE: `/api/v1/settings/notice`,
    SETTING_UNREAD_BADGE_NOTICE_CATEGORIES: `/api/v1/settings/unread-badge-notice-categories`,
    SETTING_RADIKO_LOGIN: `/api/v1/settings/radiko-login`,
    SETTING_RADIKO_LOGOUT: `/api/v1/settings/radiko-logout`,
    SETTING_RADIKO_AREA_REFRESH: `/api/v1/settings/radiko-area/refresh`,
    SETTING_EXTERNAL_IMPORT_TIMEZONE: `/api/v1/settings/external-import-timezone`,
    SETTING_STORAGE_LOW_SPACE_THRESHOLD: `/api/v1/settings/storage-low-space-threshold`,
    SETTING_MONITORING_ADVANCED: `/api/v1/settings/monitoring-advanced`,
    SETTING_MERGE_TAGS_FROM_MATCHED_RULES: `/api/v1/settings/merge-tags-from-matched-rules`,
    SETTING_EMBED_PROGRAM_IMAGE_ON_RECORD: `/api/v1/settings/embed-program-image-on-record`,
    SETTING_RESUME_PLAYBACK_ACROSS_PAGES: `/api/v1/settings/resume-playback-across-pages`,
    SETTING_RELEASE_CHECK_INTERVAL: `/api/v1/settings/release-check-interval`,
    SETTING_DUPLICATE_DETECTION_INTERVAL: `/api/v1/settings/duplicate-detection-interval`,
    EXTERNAL_IMPORT_SCAN: `/api/v1/settings/external-import/scan`,
    EXTERNAL_IMPORT_EXPORT_CSV: `/api/v1/settings/external-import/export-csv`,
    EXTERNAL_IMPORT_IMPORT_CSV: `/api/v1/settings/external-import/import-csv`,
    EXTERNAL_IMPORT_SAVE: `/api/v1/settings/external-import/save`,
    EXTERNAL_IMPORT_MAINTENANCE_SCAN_MISSING: `/api/v1/settings/external-import/maintenance/scan-missing`,
    EXTERNAL_IMPORT_MAINTENANCE_RELINK_MISSING: `/api/v1/settings/external-import/maintenance/relink-missing`,
    EXTERNAL_IMPORT_MAINTENANCE_DELETE_MISSING: `/api/v1/settings/external-import/maintenance/delete-missing`,
    SETTING_PROGRAM_UPDATE: `/api/v1/programs/update`,
    NOTIFICATION_LIST: `/api/v1/notifications`,
    NOTIFICATION_COUNT: `/api/v1/notifications/count`,
    NOTIFICATION_UNREAD: `/api/v1/notifications/latest`,
    NOTIFICATION_CLEAR: `/api/v1/notifications/clear`,
};

// チェックボックスラベルのデータを配列として定義
export const DAYS_OF_WEEK: { [key: number]: { text: string, shortText: string } } = {
    1: { text: '日曜日', shortText: '日' },
    2: { text: '月曜日', shortText: '月' },
    4: { text: '火曜日', shortText: '火' },
    8: { text: '水曜日', shortText: '水' },
    16: { text: '木曜日', shortText: '木' },
    32: { text: '金曜日', shortText: '金' },
    64: { text: '土曜日', shortText: '土' }
};


export function getDayOfWeekShortString(days: number[]) {
    let dayStrings: string[] = [];
    for (let day of days) {
        if (DAYS_OF_WEEK[day]) {
            dayStrings.push(DAYS_OF_WEEK[day].shortText);
        } else {
            throw new Error(`無効な値: ${day}`);
        }
    }
    return dayStrings.join('、');
}
