# Build the NppMarkdownPanel solution for x86 and x64 (Release).
#
# Usage:
#   .\build.ps1                                  # requires .NET 4.7.2 targeting pack
#   .\build.ps1 -FrameworkPathOverride <path>    # use NuGet reference assemblies
#     (path points to Microsoft.NETFramework.ReferenceAssemblies.net472/
#      build/.NETFramework/v4.7.2)

param(
    [string]$FrameworkPathOverride = ""
)

$ErrorActionPreference = "Stop"

# Always run from the script's directory (solution root), regardless of the
# caller's working directory.
Set-Location $PSScriptRoot

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuildpath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = join-path $msbuildpath 'MSBuild\Current\Bin\MSBuild.exe'

$extraArgs = @()
if ($FrameworkPathOverride -ne "") {
    $extraArgs += "/p:FrameworkPathOverride=$FrameworkPathOverride"
}

& $msbuild NppMarkdownPanel.sln /restore /p:RestorePackagesConfig=true /target:Build /p:Configuration=Release /p:Platform=x86 @extraArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $msbuild NppMarkdownPanel.sln /restore /p:RestorePackagesConfig=true /target:Build /p:Configuration=Release /p:Platform=x64 @extraArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
