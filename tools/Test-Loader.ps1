[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$validationRoot = Join-Path $env:TEMP ("LimitedUnderground.Loader.Validation." + [Guid]::NewGuid().ToString('N'))
$appProject = Join-Path $projectRoot 'src\LimitedUnderground.FirmwareLoader\LimitedUnderground.FirmwareLoader.csproj'
$testProject = Join-Path $projectRoot 'tests\LimitedUnderground.FirmwareLoader.Tests\LimitedUnderground.FirmwareLoader.Tests.csproj'

try {
    New-Item -ItemType Directory -Force -Path $validationRoot | Out-Null
    $env:NUGET_PACKAGES = Join-Path $validationRoot 'packages'
    $env:DOTNET_CLI_HOME = Join-Path $validationRoot 'dotnet-home'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

    dotnet build $appProject --configuration Release --artifacts-path (Join-Path $validationRoot 'app-artifacts')

    if ($LASTEXITCODE -ne 0) {
        throw "Application build failed with exit code $LASTEXITCODE."
    }

    dotnet run --project $testProject --configuration Release --artifacts-path (Join-Path $validationRoot 'test-artifacts') -- $projectRoot

    if ($LASTEXITCODE -ne 0) {
        throw "Foundation tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

Write-Host 'Shared firmware loader offline validation passed.'
