param([string]$OutputName = 'TypelessAdGuard.exe')
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$outputPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputName))
if (-not $outputPath.StartsWith(([IO.Path]::GetFullPath($PSScriptRoot) + [IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside the project' }
$null = New-Item -ItemType Directory -Force -Path (Split-Path $outputPath -Parent)
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll')
$refs += @('UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll') | ForEach-Object { Join-Path "$framework/WPF" $_ }
$arguments = @('/nologo','/codepage:65001','/warnaserror','/optimize+','/platform:x64','/target:winexe',"/out:$outputPath",("/win32manifest:" + (Join-Path $PSScriptRoot 'app.manifest')))
$arguments += $refs | ForEach-Object { "/reference:$_" }
$arguments += @('AdPolicy.cs','Native.cs','GuardEngine.cs','Configuration.cs','StatusWindow.cs','Launcher.cs','Program.cs','AssemblyInfo.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& $compiler @arguments
if ($LASTEXITCODE -ne 0) {throw 'Build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.config') -Destination ($outputPath + '.config') -Force
Write-Output "Build succeeded: $OutputName"
