param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$FrameworkPath
)
$ErrorActionPreference = 'Stop'
$msbuild = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "Install .NET Framework 4 with MSBuild, or build PowerOptionEditor.sln using an MSBuild-compatible IDE. Missing: $msbuild"
}
$buildArgs = @(
    (Join-Path $PSScriptRoot 'PowerOptionEditor.sln'),
    '/nologo', '/t:Rebuild',
    "/p:Configuration=$Configuration", '/p:Platform=x64',
    '/p:TreatWarningsAsErrors=true'
)
if ($FrameworkPath) { $buildArgs += "/p:FrameworkPathOverride=$FrameworkPath" }
& $msbuild @buildArgs
exit $LASTEXITCODE
