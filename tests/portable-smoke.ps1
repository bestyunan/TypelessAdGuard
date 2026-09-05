param([Parameter(Mandatory=$true)][string]$GuardPath, [switch]$TestReadOnly)
$ErrorActionPreference = 'Stop'
$guard = [IO.Path]::GetFullPath($GuardPath)
$root = Join-Path $PSScriptRoot ('portable-run-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $root '解压后的程序 with spaces'
$data = Join-Path $root 'user-data'
$null = New-Item -ItemType Directory -Force -Path $app
$copy = Join-Path $app 'TypelessAdGuard.exe'
Copy-Item -LiteralPath $guard -Destination $copy
Copy-Item -LiteralPath ($guard + '.config') -Destination ($copy + '.config')
function Run-Guard([string[]]$Arguments) {
    $p = Start-Process -FilePath $copy -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $p.WaitForExit(15000)) { Stop-Process -Id $p.Id; throw 'Smoke test timeout' }
    return $p.ExitCode
}
$originalAcl = $null
try {
    if ($TestReadOnly) {
        $originalAcl = Get-Acl -LiteralPath $app
        $restricted = Get-Acl -LiteralPath $app
        $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, [Security.AccessControl.FileSystemRights]::Write, [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit', [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Deny)
        $restricted.AddAccessRule($rule)
        Set-Acl -LiteralPath $app -AclObject $restricted
        $blocked = $false
        try { [IO.File]::WriteAllText((Join-Path $app 'must-not-write.txt'),'test') } catch [UnauthorizedAccessException] { $blocked = $true }
        if (-not $blocked) {throw 'Read-only test precondition failed'}
    }
    $exitCode = Run-Guard @('--data-dir',('"{0}"' -f $data),'--self-check')
    if ($exitCode -ne 0) {throw "Environment check failed: $exitCode"}
    $report = Get-Content -LiteralPath (Join-Path $data 'environment.txt') -Raw
    if ($report -notmatch 'Compatible=True') {throw 'Environment not compatible'}
    $missing = Join-Path $root 'not-installed/Typeless.exe'
    $exitCode = Run-Guard @('--data-dir',('"{0}"' -f $data),'--target',('"{0}"' -f $missing),'--probe')
    if ($exitCode -ne 0) {throw "Missing target probe failed: $exitCode"}
    if (@(Get-ChildItem -LiteralPath $app -Force).Count -ne 2) {throw 'Application wrote into executable directory'}
    Write-Output ('PASS copied directory, spaces/Unicode, runtime check, missing target, no writes beside EXE; read-only ACL=' + $TestReadOnly)
    Write-Output $report
} finally {
    if ($originalAcl) {Set-Acl -LiteralPath $app -AclObject $originalAcl}
}
