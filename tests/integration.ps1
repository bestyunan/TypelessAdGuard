param([string[]]$Scenarios = @('exact','high-demand','deep','changed','blocked'), [string]$GuardPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$fixture = Join-Path $PSScriptRoot 'TypelessTestHost.exe'
$refs = @('System.dll','System.Core.dll','System.Xaml.dll')
$refs += @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','UIAutomationTypes.dll') | ForEach-Object {Join-Path "$framework/WPF" $_}
$compilerArgs = @('/nologo','/codepage:65001','/warnaserror','/target:winexe',"/out:$fixture")
$compilerArgs += $refs | ForEach-Object {"/reference:$_"}
$compilerArgs += Join-Path $PSScriptRoot 'Fixture.cs'
& "$framework/csc.exe" @compilerArgs
if ($LASTEXITCODE -ne 0) {throw 'Fixture build failed'}
$guard = if ($GuardPath) {[IO.Path]::GetFullPath($GuardPath)} else {Join-Path $project 'TypelessAdGuard.exe'}
$runtime = Join-Path $PSScriptRoot ('runtime-' + [Guid]::NewGuid().ToString('N'))
$sha = [Security.Cryptography.SHA256]::Create()
try { $tag = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($fixture.ToUpperInvariant())))).Replace('-','').Substring(0,16) } finally { $sha.Dispose() }
foreach ($scenario in $Scenarios) {
    $result = Join-Path $PSScriptRoot "$scenario-result.txt"
    Set-Content -LiteralPath $result -Value ''
    $hostProcess = Start-Process -FilePath $fixture -ArgumentList @(('"{0}"' -f $result),$scenario) -WindowStyle Hidden -PassThru
    $guardProcess = $null
    $guardLog = Join-Path $runtime "logs/guard-$tag.log"
    $beforeLines = if (Test-Path -LiteralPath $guardLog) { @(Get-Content -LiteralPath $guardLog).Count } else { 0 }
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(4)
        while ((Get-Content -LiteralPath $result -Raw) -notmatch 'READY') {
            if ([DateTime]::UtcNow -gt $deadline) {throw 'Fixture did not open'}
            Start-Sleep -Milliseconds 100
        }
        $guardProcess = Start-Process -FilePath $guard -ArgumentList @('--target',('"{0}"' -f $fixture),'--data-dir',('"{0}"' -f $runtime),'--background') -WindowStyle Hidden -PassThru
        if (-not $hostProcess.WaitForExit(14000)) {throw 'Fixture timed out'}
        $content = Get-Content -LiteralPath $result -Raw
        Write-Output "$scenario`: $content"
        if ($content -notmatch 'PASS' -or $content -match 'FAIL|WRONG_UPGRADE') {throw "Integration failed: $scenario"}
        $newLog = (Get-Content -LiteralPath $guardLog | Select-Object -Skip $beforeLines) -join "`n"
        if ($scenario -eq 'blocked' -and $newLog -match 'CONFIRMED promotion_gone') {throw 'False success: blocked button did not dismiss promotion'}
    } finally {
        $stopper = Start-Process -FilePath $guard -ArgumentList @('--target',('"{0}"' -f $fixture),'--data-dir',('"{0}"' -f $runtime),'--stop') -WindowStyle Hidden -PassThru
        $null = $stopper.WaitForExit(3000)
        if ($guardProcess -and -not $guardProcess.HasExited -and -not $guardProcess.WaitForExit(4000)) {Stop-Process -Id $guardProcess.Id}
        if (-not $hostProcess.HasExited) {Stop-Process -Id $hostProcess.Id}
    }
}
