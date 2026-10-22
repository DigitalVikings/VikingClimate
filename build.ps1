[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [switch]$Rebuild,
    [switch]$Package,
    [switch]$Clean,           # Cleans other polluting dev mods from profile + local build folders
    [switch]$CleanOtherMods,  # Explicit alias to purge other dev mods from profile
    [switch]$CleanThisMod,    # Cleans this mod's deployed plugin folder
    [switch]$PurgeConfig      # Also purges this mod's .cfg file
)

$ErrorActionPreference = "Stop"

# Resolve active profile from Environment.props
$profile = "DEV"
$envPropsPath = Join-Path $PSScriptRoot "Environment.props"
if (Test-Path $envPropsPath) {
    try {
        [xml]$xml = Get-Content $envPropsPath
        if ($xml.Project.PropertyGroup.R2ModmanProfile) {
            $profile = $xml.Project.PropertyGroup.R2ModmanProfile.Trim()
        }
    } catch { }
}

$pluginsDir = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\$profile\BepInEx\plugins"
$modsYml = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\$profile\mods.yml"
$thisDeployDir = Join-Path $pluginsDir "VikingClimate"
$thisConfigPath = "$env:APPDATA\r2modmanPlus-local\Valheim\profiles\$profile\BepInEx\config\com.digitalvikings.vikingclimate.cfg"

if ($CleanThisMod) {
    Write-Host "Cleaning VikingClimate deployment from profile '$profile'..." -ForegroundColor Cyan

    if (Test-Path $thisDeployDir) {
        Remove-Item -Recurse -Force $thisDeployDir
        Write-Host "[Clean] Removed deployed plugin: $thisDeployDir" -ForegroundColor Yellow
    }

    if ($PurgeConfig -and (Test-Path $thisConfigPath)) {
        Remove-Item -Force $thisConfigPath
        Write-Host "[Clean] Purged mod configuration: $thisConfigPath" -ForegroundColor Yellow
    }

    @("bin", "obj", "dist") | ForEach-Object {
        $p = Join-Path $PSScriptRoot $_
        if (Test-Path $p) { Remove-Item -Recurse -Force $p; Write-Host "[Clean] Removed local $_/" -ForegroundColor Gray }
    }

    Write-Host "CleanThisMod completed successfully!" -ForegroundColor Green
    return
}

if ($Clean -or $CleanOtherMods) {
    Write-Host "Scanning profile '$profile' for other unmanaged dev mods to purge..." -ForegroundColor Cyan

    $managedNames = @()
    if (Test-Path $modsYml) {
        $managedNames = (Get-Content $modsYml | Select-String 'name:\s+(.+)' | ForEach-Object { $_.Matches.Groups[1].Value.Trim() })
    }

    if (Test-Path $pluginsDir) {
        $allFolders = Get-ChildItem $pluginsDir -Directory
        $pollutingMods = $allFolders | Where-Object { 
            $_.Name -notin $managedNames -and $_.Name -ne "VikingSurvival" 
        }

        if ($pollutingMods) {
            foreach ($folder in $pollutingMods) {
                Write-Host "[Clean] Purging polluting dev mod: $($folder.Name)" -ForegroundColor Yellow
                Remove-Item -Recurse -Force $folder.FullName
            }
        } else {
            Write-Host "[Clean] No other polluting dev mods found in '$profile'. Profile is clean!" -ForegroundColor Green
        }
    }

    @("bin", "obj", "dist") | ForEach-Object {
        $localPath = Join-Path $PSScriptRoot $_
        if (Test-Path $localPath) {
            Remove-Item -Recurse -Force $localPath
            Write-Host "[Clean] Removed local folder: $_/" -ForegroundColor Gray
        }
    }

    Write-Host "Clean completed! All 3rd-party dependencies preserved intact." -ForegroundColor Green
    return
}

# Ensure user-installed .NET 8 SDK is used if present
$userDotNet = "$env:LocalAppData\Microsoft\dotnet"
if (Test-Path "$userDotNet\dotnet.exe") {
    $env:DOTNET_ROOT = $userDotNet
    $dotnetExe = "$userDotNet\dotnet.exe"
    $env:Path = "$userDotNet;$env:Path"
} else {
    $dotnetExe = "dotnet"
}

Push-Location $PSScriptRoot
try {
    Write-Host "Building VikingClimate [$Configuration]..." -ForegroundColor Cyan

    $buildArgs = @("build", "$PSScriptRoot\VikingClimate.csproj", "-c", $Configuration)
    if ($Rebuild) {
        $buildArgs += "--no-incremental"
    }

    & $dotnetExe @buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Build and deployment completed successfully!" -ForegroundColor Green

if ($Package) {
    Write-Host "Packaging Thunderstore release zip..." -ForegroundColor Cyan
    
    $manifestPath = Join-Path $PSScriptRoot "manifest.json"
    $iconPath = Join-Path $PSScriptRoot "icon.png"
    $readmePath = Join-Path $PSScriptRoot "README.md"
    
    if (-not (Test-Path $manifestPath)) { throw "Missing manifest.json" }
    if (-not (Test-Path $iconPath)) { throw "Missing icon.png" }
    if (-not (Test-Path $readmePath)) { throw "Missing README.md" }

    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $version = $manifest.version_number
    $modName = $manifest.name

    $targetDll = Join-Path $PSScriptRoot "bin\$Configuration\netstandard2.1\$modName.dll"
    $targetPdb = Join-Path $PSScriptRoot "bin\$Configuration\netstandard2.1\$modName.pdb"
    if (-not (Test-Path $targetDll)) {
        $dllFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "bin\$Configuration\netstandard2.1") -Filter "*.dll"
        if ($dllFiles.Count -gt 0) {
            $targetDll = $dllFiles[0].FullName
            $targetPdb = [System.IO.Path]::ChangeExtension($targetDll, ".pdb")
        } else {
            throw "Missing compiled assembly in bin\$Configuration\netstandard2.1\"
        }
    }

    $distDir = Join-Path $PSScriptRoot "dist"
    if (-not (Test-Path $distDir)) {
        New-Item -ItemType Directory -Path $distDir | Out-Null
    }

    $stagingDir = Join-Path $distDir "staging"
    if (Test-Path $stagingDir) {
        Remove-Item -Recurse -Force $stagingDir
    }
    New-Item -ItemType Directory -Path $stagingDir | Out-Null

    Copy-Item $manifestPath -Destination $stagingDir
    Copy-Item $iconPath -Destination $stagingDir
    Copy-Item $readmePath -Destination $stagingDir
    Copy-Item $targetDll -Destination $stagingDir
    if (Test-Path $targetPdb) {
        Copy-Item $targetPdb -Destination $stagingDir
    }

        Get-ChildItem -Path $PSScriptRoot -File -Filter "*.png" | Where-Object { $_.Name -ne "icon.png" } | ForEach-Object {
        Copy-Item $_.FullName -Destination $stagingDir
    }

    $zipFileName = "$modName-$version.zip"
    $zipPath = Join-Path $distDir $zipFileName
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }

    Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -Force
    Remove-Item -Recurse -Force $stagingDir

    Write-Host "[Package] Created release zip: $zipPath" -ForegroundColor Green
}
} finally {
    Pop-Location
}
