import { RadioServiceKind, RecordingType, ReserveType, AvailabilityTimeFree } from './define.js';

export interface Station {
    areaId: string;
    areaName: string;
    stationId: string;
    stationName: string;
}

export interface DateElement {
    value: string;
    textContent: string;
    isToday: boolean;
}

export interface Program {
    programId: string;
    serviceKind: RadioServiceKind;
    areaId: string;
    areaName: string;
    stationId: string;
    stationName: string;
    title: string;
    performer: string;
    description: string;
    startTime: string;
    endTime: string;
    availabilityTimeFree: AvailabilityTimeFree;
    onDemandContentUrl?: string | null;
    onDemandExpiresAtUtc?: string | null;

}

export interface StationData {
    [region: string]: Station[];
}

export interface SearchData {
    SelectedRadikoStationIds: string[];
    SelectedRadiruStationIds: string[];
    Keyword: string;
    SearchTitleOnly: boolean;
    ExcludedKeyword: string;
    ExcludeTitleOnly: boolean;
    SelectedDaysOfWeek: number[];
    StartTime: string;
    EndTime: string;
    IncludeHistoricalPrograms: boolean;
    orderKind: string;
}
export interface KeywordReserveData {
    Id?: string;
    SortOrder?: number;
    SelectedRadikoStationIds: string[];
    SelectedRadiruStationIds: string[];
    Keyword: string;
    SearchTitleOnly: boolean;
    ExcludedKeyword: string;
    ExcludeTitleOnly: boolean;
    SelectedDaysOfWeek: number[];
    RecordPath: string;
    RecordFileName: string;
    StartTimeString: string;
    EndTimeString: string;
    IsEnabled?: boolean;
    StartDelay?: number;
    EndDelay?: number;
    TagIds?: string[];
    MergeTagBehavior?: number;
}

export interface Recording {
    id: string;
    title: string;
    stationName: string;
    startDateTime: string;
    endDateTime: string;
    duration: number;
    tags: string[];
    isListened: boolean;
}

export interface RecordedStationFilter {
    stationId: string;
    stationName: string;
}

export interface RecordedDuplicateSide {
    recordingId: string;
    title: string;
    stationId: string;
    stationName: string;
    startDateTime: string;
    endDateTime: string;
    durationSeconds: number;
}

export interface RecordedDuplicateCandidate {
    left: RecordedDuplicateSide;
    right: RecordedDuplicateSide;
    phase1Score: number;
    audioScore: number;
    finalScore: number;
    startTimeDiffHours: number;
    durationDiffSeconds: number;
}

export interface RecordedDuplicateDetectionStatus {
    isRunning: boolean;
    lastStartedAtUtc?: string | null;
    lastCompletedAtUtc?: string | null;
    lastSucceeded: boolean;
    lastMessage: string;
}
export interface ProgramReserve {
    id: string;
    programId: string;
    title: string;
    startDateTime: string;
    endDateTime: string;
    stationId: string;
    stationName: string;
    areaId: string;
    areaName: string;
    serviceKind: number;
    isEnabled: boolean;
    recordingType: RecordingType;
    reserveType: ReserveType;
    matchedKeywordReserveKeywords: string[];
    plannedTagNames: string[];
}

export interface KeywordReserve {
    id: string;
    sortOrder: number;
    selectedRadikoStationIds: string[];
    selectedRadiruStationIds: string[];
    keyword: string;
    searchTitleOnly: boolean;
    excludedKeyword: string;
    excludeTitleOnly: boolean;
    recordPath: string;
    recordFileName: string;
    selectedDaysOfWeek: number[];
    startTime: string;
    endTime: string;
    isEnabled: boolean;
    startDelay?: number;
    endDelay?: number;
    tagIds?: string[];
    tags?: string[];
    mergeTagBehavior?: number;
}

export interface Tag {
    id: string;
    name: string;
    recordingCount: number;
    lastUsedAt?: string | null;
    createdAt: string;
}

export interface TagBulkOperationResult {
    successCount: number;
    skipCount: number;
    failCount: number;
    failedRecordingIds: string[];
}

export interface RecordingBulkDeleteResult {
    successCount: number;
    skipCount?: number;
    failCount: number;
    failedRecordingIds: string[];
}

export interface RecordingBulkListenedResult {
    successCount: number;
    skipCount: number;
    failCount: number;
    failedRecordingIds: string[];
    skippedRecordingIds: string[];
}

export interface Notification {
    timestamp: string;
    message: string;
}

export interface ApiError {
    code?: string;
    message: string;
}

export interface ApiResponse<T> {
    success: boolean;
    data: T;
    error?: ApiError | null;
    message?: string | null;
}

export interface Result {
    message: string;
}

export interface ExternalImportCandidate {
    isSelected: boolean;
    filePath: string;
    title: string;
    description: string;
    stationName: string;
    broadcastAt: string;
    tags: string[];
}

export interface ExternalImportValidationError {
    filePath: string;
    message: string;
}

export interface ExternalImportSaveResult {
    savedCount: number;
    errors: ExternalImportValidationError[];
}

export interface RecordingFileMaintenanceEntry {
    recordingId: string;
    title: string;
    stationName: string;
    storedPath: string;
    fileName: string;
    candidateCount: number;
    candidateRelativePaths: string[];
    isSelected?: boolean;
}

export interface RecordingFileMaintenanceScanResult {
    missingCount: number;
    entries: RecordingFileMaintenanceEntry[];
}

export interface RecordingFileMaintenanceActionDetail {
    recordingId: string;
    status: string;
    message: string;
}

export interface RecordingFileMaintenanceActionResult {
    targetCount: number;
    successCount: number;
    skipCount: number;
    failCount: number;
    details: RecordingFileMaintenanceActionDetail[];
}
