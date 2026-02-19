param(
    [Parameter(Mandatory = $true)]
    [string]$RecordDir,

    [string]$TempDir,

    [string]$AppSettingsPath = ".\RadiKeep\appsettings.json",

    [switch]$CreateDirectories,

    [switch]$RestartService,

    [string]$ServiceName = 'RadiKeep'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$recordKey = 'RecordFileSaveFolder'
$tempKey = 'TemporaryFileSaveFolder'
$generalOptionsKey = 'GeneralOptions'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-ElevatedSelf {
    if (Test-IsAdministrator) {
        return
    }

    # Only needed when service restart is requested.
    Write-Host "管理者権限で set-radikeep-storage-settings.ps1 を再実行します..."

    if ([string]::IsNullOrWhiteSpace($PSCommandPath)) {
        throw "スクリプトパスを取得できませんでした。スクリプトファイルとして実行してください。"
    }

    $shellPath = (Get-Process -Id $PID).Path
    if ([string]::IsNullOrWhiteSpace($shellPath)) {
        $shellPath = "pwsh.exe"
    }

    $argumentList = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $PSCommandPath
    )

    if ($MyInvocation.UnboundArguments) {
        $argumentList += $MyInvocation.UnboundArguments
    }

    Start-Process -FilePath $shellPath -Verb RunAs -ArgumentList $argumentList | Out-Null
    exit 0
}

if ($CreateDirectories) {
    New-Item -ItemType Directory -Path $RecordDir -Force | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($TempDir)) {
        New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    }
}

if (-not (Test-Path -Path $AppSettingsPath)) {
    throw "appsettings.json が見つかりません: $AppSettingsPath"
}

# Load and patch only the GeneralOptions storage keys.
$appsettings = Get-Content -Raw -Path $AppSettingsPath | ConvertFrom-Json
if ($null -eq $appsettings) {
    throw "JSON の解析に失敗しました: $AppSettingsPath"
}

if (-not ($appsettings.PSObject.Properties.Name -contains $generalOptionsKey)) {
    $appsettings | Add-Member -MemberType NoteProperty -Name $generalOptionsKey -Value ([pscustomobject]@{})
}

$appsettings.GeneralOptions | Add-Member -MemberType NoteProperty -Name $recordKey -Value $RecordDir -Force
if (-not [string]::IsNullOrWhiteSpace($TempDir)) {
    $appsettings.GeneralOptions | Add-Member -MemberType NoteProperty -Name $tempKey -Value $TempDir -Force
}

$json = $appsettings | ConvertTo-Json -Depth 10
Set-Content -Path $AppSettingsPath -Value $json -Encoding UTF8

Write-Host "appsettings を更新しました: $AppSettingsPath"
Write-Host "GeneralOptions.$recordKey = $RecordDir"
if (-not [string]::IsNullOrWhiteSpace($TempDir)) {
    Write-Host "GeneralOptions.$tempKey = $TempDir"
}

if ($RestartService) {
    # Service restart applies updated appsettings without manual operation.
    Ensure-ElevatedSelf

    $task = Get-ScheduledTask -TaskName $ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $task) {
        Stop-ScheduledTask -TaskName $ServiceName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
        Start-ScheduledTask -TaskName $ServiceName
        Write-Host "タスク '$ServiceName' を再起動しました。"
        return
    }

    throw "タスク '$ServiceName' が見つかりません。"
}
