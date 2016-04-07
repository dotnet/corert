# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param (
    [string] $InstallDir = $null,
    [string] $TargetPlatform = "x64"
)

# The below code is from dotnet/cli install.ps1

$ErrorActionPreference="Stop"
$ProgressPreference="SilentlyContinue"

$Feed="https://dotnetcli.blob.core.windows.net/dotnet"
$Channel="beta"
$DotNetFileName="dotnet-dev-win-" + $TargetPlatform + ".1.0.0-beta-002228.zip"
$DotNetUrl="$Feed/$Channel/Binaries/1.0.0-beta-002228"

function say($str)
{
    Write-Host "dotnet_install: $str"
}

# Should be kept in sync with CLI's implementation (cli/scripts/obtain/install.ps1)
function Get-Version-Info-From-Version-Text([string]$VersionText)
{
    $Data = @($VersionText.Split([char[]]@(), [StringSplitOptions]::RemoveEmptyEntries));

    $VersionInfo = @{}
    $VersionInfo.CommitHash = $Data[0].Trim()
    $VersionInfo.Version = $Data[1].Trim()
    return $VersionInfo
}

if (!$InstallDir) {
    $InstallDir = "$env:LocalAppData\Microsoft\dotnet"
}

say "Preparing to install .NET Tools to $InstallDir"

# Check if we need to bother
$CliSdkFolder = "$InstallDir\cli\sdk"
if (Test-Path $CliSdkFolder)
{
  $SdkFolders = Get-ChildItem -Directory  $CliSdkFolder
  foreach ($folder in $SdkFolders)
  {
    $LocalFile = Join-Path $folder.FullName ".version"
    if (Test-Path $LocalFile)
    {
      break
    }
  }
}

if ($LocalFile -And (Test-Path $LocalFile))
{
    $LocalData = @(cat $LocalFile)
    $LocalHash = $LocalData[0].Trim()
    $LocalVersion = $LocalData[1].Trim()
    if ($LocalVersion -and $LocalHash)
    {
        say "$Feed/$Channel/dnvm/latest.win.$TargetPlatform.version"
        $RemoteResponse = Invoke-WebRequest -UseBasicParsing "$Feed/$Channel/dnvm/latest.win.$TargetPlatform.version"
        $RemoteVersionText = [Text.Encoding]::UTF8.GetString($RemoteResponse.Content)
        $RemoteVersionInfo = Get-Version-Info-From-Version-Text $RemoteVersionText
        $RemoteHash = $RemoteVersionInfo.CommitHash
        $RemoteVersion = $RemoteVersionInfo.Version
        if (!$RemoteVersion -or !$RemoteHash) {
            throw "Invalid response from feed"
        }

        say "Latest version: $RemoteVersion"
        say "Local Version: $LocalVersion"

        if($LocalHash -eq $RemoteHash)
        {
            say "You already have the latest version"
            exit 0
        }
    }
}

# Set up the install location
if (!(Test-Path $InstallDir)) {
    mkdir $InstallDir | Out-Null
}

# De-powershell the path before passing to .NET APIs
$InstallDir = Convert-Path $InstallDir

say "Downloading $DotNetFileName from $DotNetUrl"
$resp = Invoke-WebRequest -UseBasicParsing "$DotNetUrl/$DotNetFileName" -OutFile "$InstallDir\$DotNetFileName"

say "Extracting zip"

# Create the destination
if (Test-Path "$InstallDir\cli_new") {
    del -rec -for "$InstallDir\cli_new"
}
mkdir "$InstallDir\cli_new" | Out-Null

Add-Type -Assembly System.IO.Compression.FileSystem | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory("$InstallDir\$DotNetFileName", "$InstallDir\cli_new")

# Replace the old installation (if any)
if (Test-Path "$InstallDir\cli") {
    del -rec -for "$InstallDir\cli"
}
mv "$InstallDir\cli_new" "$InstallDir\cli"

# Clean the zip
if (Test-Path "$InstallDir\$DotNetFileName") {
    del -for "$InstallDir\$DotNetFileName"
}

say "The .NET Tools have been installed to $InstallDir\cli!"

# New layout
say "Add '$InstallDir\cli' to your PATH to use dotnet"

