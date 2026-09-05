$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testOut = Join-Path $PSScriptRoot 'tests/PortableTests.exe'
& $compiler /nologo /codepage:65001 /warnaserror /target:exe "/out:$testOut" (Join-Path $PSScriptRoot 'Configuration.cs') (Join-Path $PSScriptRoot 'tests/PortableTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Portable test compilation failed' }
& $testOut
exit $LASTEXITCODE
