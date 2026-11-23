[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [switch]$Rebuild,
    [switch]$Package,
    [switch]$SkipAudit,
    [switch]$Clean,
    [switch]$CleanOtherMods,
    [switch]$CleanThisMod,
    [switch]$PurgeConfig
)

$sharedRunner = Join-Path $PSScriptRoot "..\.agent\scripts\invoke-mod-build.ps1"
if (-not (Test-Path $sharedRunner)) {
    throw "Shared build runner not found at: $sharedRunner"
}
& $sharedRunner -ModDir $PSScriptRoot @PSBoundParameters
