param(
    # Native rustrender.dll produced by cargo. The architecture must match
    # the Notepad++ build each zip targets (x64 NPP needs the x64 DLL).
    [string]$RustRenderDllX64 = "..\target\x86_64-pc-windows-msvc\release\rustrender.dll",
    [string]$RustRenderDllX86 = "..\target\i686-pc-windows-msvc\release\rustrender.dll"
)

if (Test-Path Release\lib\) {Remove-Item Release\lib\ -Recurse -Force}
New-Item "Release\lib\" -itemType Directory
Copy-Item -Force -Recurse -Path MarkdigWrapper\bin\Release\*.dll -Destination Release\lib\
Copy-Item -Force -Recurse -Path RustRenderWrapper\bin\Release\*.dll -Destination Release\lib\
Copy-Item -Force -Recurse -Path PanelCommon\bin\Release\*.dll -Destination Release\lib\
Copy-Item -Force -Recurse -Path Webview2Viewer\bin\Release\*.dll -Destination Release\lib\
Copy-Item -Force -Recurse -Path Webview2Viewer\bin\Release\runtimes\ -Destination Release\lib\runtimes\

function makeReleaseZip($filename, $targetPlattform, $rustRenderDll)
{
	$zipName = "Release\NppMarkdownPanel-" + (Get-Item $filename).VersionInfo.FileVersion + "-" + $targetPlattform + ".zip"
	$items = @($filename, 'Release\lib\', 'README.md', 'help\', 'License.txt', "NppMarkdownPanel\style.css" , "NppMarkdownPanel\style-dark.css", "NppMarkdownPanel\style-themes.css")
	if ($rustRenderDll -and (Test-Path $rustRenderDll)) { $items += $rustRenderDll }
	else { Write-Warning "rustrender.dll not found ($rustRenderDll) - packing $zipName without the native renderer (Markdig fallback)." }
	Compress-Archive -LiteralPath $items -DestinationPath $zipName -Force
}

makeReleaseZip "NppMarkdownPanel\bin\Release\NppMarkdownPanel.dll" "x86" $RustRenderDllX86
makeReleaseZip "NppMarkdownPanel\bin\Release-x64\NppMarkdownPanel.dll" "x64" $RustRenderDllX64
pause
