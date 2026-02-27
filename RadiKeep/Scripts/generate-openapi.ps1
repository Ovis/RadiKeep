param(
    [string]$BaseUrl = "https://127.0.0.1:7262"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot "openapi"
$outputPath = Join-Path $outputDir "openapi.json"

if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$openApiUrl = "$BaseUrl/openapi/v1.json"

try {
    $res = Invoke-WebRequest -Uri $openApiUrl -UseBasicParsing -TimeoutSec 10
    if ($res.StatusCode -ne 200 -or [string]::IsNullOrWhiteSpace($res.Content)) {
        throw "OpenAPI endpoint returned empty response: $openApiUrl"
    }

    Set-Content -Path $outputPath -Value $res.Content -Encoding UTF8
    Write-Host "OpenAPI document generated: $outputPath"
}
catch {
    throw "OpenAPI取得に失敗しました。先にアプリを起動してください。 endpoint=$openApiUrl"
}
