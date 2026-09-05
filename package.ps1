param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$version = '0.1.0-preview.1'
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -OutputName 'artifacts/build/TypelessAdGuard.exe' }
$built = Join-Path $PSScriptRoot 'artifacts/build/TypelessAdGuard.exe'
if (-not (Test-Path -LiteralPath $built)) {throw 'Build missing'}
$stage = Join-Path $PSScriptRoot ('artifacts/staging/' + [Guid]::NewGuid().ToString('N'))
$binaryName = "TypelessAdGuard-$version-windows-x64"
$sourceName = "TypelessAdGuard-$version-source"
$binary = Join-Path $stage $binaryName
$source = Join-Path $stage $sourceName
$release = Join-Path $PSScriptRoot "artifacts/releases/$version"
$null = New-Item -ItemType Directory -Force -Path $binary,$source,$release
function Copy-Listed([string]$From, [string]$To, [string[]]$Files) {
    foreach ($file in $Files) {
        $destination = Join-Path $To $file
        $null = New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent)
        Copy-Item -LiteralPath (Join-Path $From $file) -Destination $destination
    }
}
Copy-Listed (Split-Path $built -Parent) $binary @('TypelessAdGuard.exe','TypelessAdGuard.exe.config')
$publicDocs = @('README.md','LICENSE','assets/cover.png','docs/COMPATIBILITY.md','docs/RELEASING.md')
Copy-Listed $PSScriptRoot $binary $publicDocs
$sourceFiles = $publicDocs + @(
    '.gitignore','.github/workflows/build.yml','AdPolicy.cs','AssemblyInfo.cs','Configuration.cs','GuardEngine.cs','Launcher.cs','Native.cs','Program.cs','StatusWindow.cs',
    'app.config','app.manifest','build.ps1','package.ps1','test-policy.ps1','test-portable.ps1',
    'tests/PolicyTests.cs','tests/PortableTests.cs','tests/Fixture.cs','tests/integration.ps1','tests/portable-smoke.ps1'
)
Copy-Listed $PSScriptRoot $source $sourceFiles
# ZipFile includes dotfiles such as .github and .gitignore; Compress-Archive can omit hidden entries.
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-Zip([string]$Folder,[string]$Name) {
    $temporary = Join-Path $stage ($Name + '.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($Folder,$temporary,[IO.Compression.CompressionLevel]::Optimal,$true)
    Copy-Item -LiteralPath $temporary -Destination (Join-Path $release ($Name + '.zip')) -Force
}
Write-Zip $binary $binaryName
Write-Zip $source $sourceName
$hashes = foreach ($name in @($binaryName,$sourceName)) {
    $file = Join-Path $release ($name + '.zip')
    (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name + '.zip'
}
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'),[string[]]$hashes,[Text.Encoding]::ASCII)
[IO.File]::WriteAllLines((Join-Path $release 'SOURCE-MANIFEST.txt'),[string[]]($sourceFiles | Sort-Object),[Text.Encoding]::UTF8)
[IO.File]::WriteAllText((Join-Path $release 'RELEASE-NOTES.txt'),"0.1.0-preview.1`r`nWindows 10/11 x64; .NET Framework 4.8+.`r`nMIT license. No runtime logs or personal captures included.`r`nIncludes target discovery, path picker, connection status, per-user data and two promotion rules.`r`nIndependent machine/clean VM validation remains pending. This is a preview, not a stable compatibility claim.`r`n",[Text.Encoding]::UTF8)
Write-Output "Binary ZIP: $(Join-Path $release ($binaryName + '.zip'))"
Write-Output "Source ZIP: $(Join-Path $release ($sourceName + '.zip'))"
Write-Output "Source folder: $source"
