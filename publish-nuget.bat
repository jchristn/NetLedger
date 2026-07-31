@echo off
setlocal EnableExtensions

if "%~1"=="" (
    echo ERROR: NuGet API key is required.
    echo.
    echo Usage: publish-nuget ^<apikey^>
    echo Example: publish-nuget your-nuget-api-key
    exit /b 1
)

if not "%~2"=="" (
    echo ERROR: Only one argument is supported.
    echo.
    echo Usage: publish-nuget ^<apikey^>
    exit /b 1
)

call dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet CLI is not installed or is not in PATH.
    exit /b 1
)

set "NUGET_API_KEY=%~1"
set "NUGET_SOURCE=https://api.nuget.org/v3/index.json"
set "REPO_ROOT=%~dp0"
set "PUBLISH_NUGET_SCRIPT=%~f0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $content=Get-Content -LiteralPath $env:PUBLISH_NUGET_SCRIPT -Raw; $marker=':POWERSHELL'; $index=$content.LastIndexOf($marker); if ($index -lt 0) { throw 'PowerShell payload marker not found.' }; Invoke-Expression $content.Substring($index + $marker.Length)"
exit /b %ERRORLEVEL%

:POWERSHELL
$ErrorActionPreference = 'Stop'

$repoRoot = $env:REPO_ROOT
$source = $env:NUGET_SOURCE
$apiKey = $env:NUGET_API_KEY

$packages = @(
    @{ Id = 'NetLedger'; Dir = 'src\NetLedger\bin\Release' },
    @{ Id = 'NetLedger.Archive'; Dir = 'src\NetLedger.Archive\bin\Release' },
    @{ Id = 'NetLedger.Sdk'; Dir = 'sdk\sdk-csharp\NetLedger.Sdk\bin\Release' }
)

function Get-LatestPackagePair {
    param(
        [Parameter(Mandatory = $true)][string] $Id,
        [Parameter(Mandatory = $true)][string] $RelativeDir
    )

    $dir = Join-Path $repoRoot $RelativeDir
    if (-not (Test-Path -LiteralPath $dir)) {
        throw "Package directory not found: $RelativeDir. Run dotnet build src\NetLedger.sln -c Release first."
    }

    $regex = '^' + [regex]::Escape($Id) + '\.(?<version>\d+(?:\.\d+){1,3})\.nupkg$'
    $candidates = @()

    foreach ($file in Get-ChildItem -LiteralPath $dir -File -Filter ($Id + '.*.nupkg')) {
        if ($file.Name -match $regex) {
            $candidates += [pscustomobject]@{
                Id = $Id
                Version = [version]$Matches.version
                VersionText = $Matches.version
                Nupkg = $file.FullName
            }
        }
    }

    $latest = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -eq $latest) {
        throw "No release .nupkg found for $Id in $RelativeDir. Run dotnet build src\NetLedger.sln -c Release first."
    }

    $snupkg = Join-Path $dir ($Id + '.' + $latest.VersionText + '.snupkg')
    if (-not (Test-Path -LiteralPath $snupkg)) {
        throw "Matching symbol package not found: $snupkg"
    }

    $latest | Add-Member -NotePropertyName Snupkg -NotePropertyValue $snupkg
    return $latest
}

$resolved = foreach ($package in $packages) {
    Get-LatestPackagePair $package['Id'] $package['Dir']
}

Write-Host "Publishing latest NuGet packages to $source"
foreach ($package in $resolved) {
    Write-Host ('  {0} {1}' -f $package.Id, $package.VersionText)
    Write-Host ('    ' + $package.Nupkg)
    Write-Host ('    ' + $package.Snupkg)
}

foreach ($package in $resolved) {
    Write-Host ('Publishing ' + [IO.Path]::GetFileName($package.Nupkg))
    & dotnet nuget push $package.Nupkg --source $source --api-key $apiKey --skip-duplicate --no-symbols --force-english-output
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ('Publishing ' + [IO.Path]::GetFileName($package.Snupkg))
    & dotnet nuget push $package.Snupkg --source $source --api-key $apiKey --skip-duplicate --force-english-output
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host 'NuGet package publishing completed successfully.'
