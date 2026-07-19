[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Full', 'AppOnly', 'Upgrade', 'Uninstall')]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [string]$SqlServer,

    [Parameter(Mandatory = $true)]
    [string]$Database,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [Parameter(Mandatory = $true)]
    [string]$LogDirectory,

    [ValidateSet('Windows', 'SqlPassword')]
    [string]$Authentication = 'Windows',

    [System.Management.Automation.PSCredential]
    $SqlCredential,

    [System.Management.Automation.PSCredential]
    $BootstrapAdminCredential,

    [switch]$ConfirmDisposableMachine,
    [switch]$AllowUnsignedTestBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ConfirmDisposableMachine) {
    throw 'Smoke test chi duoc chay tren may ao hoac may thu co the khoi phuc snapshot. Dung -ConfirmDisposableMachine de xac nhan.'
}

$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$resolvedLogDirectory = [System.IO.Path]::GetFullPath($LogDirectory)
[System.IO.Directory]::CreateDirectory($resolvedLogDirectory) | Out-Null

$applicationDirectory = Join-Path $env:ProgramFiles 'WarePro'
$applicationPath = Join-Path $applicationDirectory 'WarePro.exe'
$settingsPath = Join-Path $env:ProgramData 'WarePro\Config\warepro.settings.json'
$localDataDirectory = Join-Path $env:LOCALAPPDATA 'WarePro'
$runId = "{0}-{1}-{2}" -f $Mode.ToLowerInvariant(), (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$installerLog = Join-Path $resolvedLogDirectory "installer-$runId.log"
$evidencePath = Join-Path $resolvedLogDirectory "evidence-$runId.json"
$setupHelperPath = Join-Path $applicationDirectory 'Setup\WarePro.SetupHelper.exe'
$setupHelperLog = Join-Path $resolvedLogDirectory "setup-helper-$runId.log"
$sqlServiceName = 'MSSQL$SQLEXPRESS'
$pendingInstallerRegistryPath = 'HKLM:\SOFTWARE\WarePro\Installer'
$pendingBefore = 1 -eq (Get-ItemProperty -LiteralPath $pendingInstallerRegistryPath -Name Pending -ErrorAction SilentlyContinue).Pending
$machineSetupHelperLog = Join-Path $env:ProgramData 'WarePro\InstallerLogs\setup-helper.log'
function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-SqlServiceEvidence {
    $service = Get-Service -Name $sqlServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        return [ordered]@{ exists = $false; status = $null }
    }

    return [ordered]@{ exists = $true; status = $service.Status.ToString() }
}

function Assert-InstallerSignature {
    if ($AllowUnsignedTestBuild) {
        return [ordered]@{ status = 'SkippedForTestBuild'; signer = $null }
    }

    $signature = Get-AuthenticodeSignature -FilePath $resolvedInstaller
    if ($signature.Status -ne 'Valid') {
        throw "Installer signature khong hop le: $($signature.Status)."
    }

    return [ordered]@{
        status = $signature.Status.ToString()
        signer = $signature.SignerCertificate.Subject
        thumbprint = $signature.SignerCertificate.Thumbprint
    }
}

function Invoke-ProcessAndCheckExitCode {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) {
        throw "Tien trinh tra ve ma loi $($process.ExitCode). Xem log: $installerLog"
    }

    return $process.ExitCode
}

function Assert-InstalledState {
    if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
        throw "Khong tim thay WarePro.exe tai $applicationPath."
    }

    $actualVersion = (Get-Item -LiteralPath $applicationPath).VersionInfo.ProductVersion
    if ([version]$actualVersion -ne [version]$ExpectedVersion) {
        throw "Sai phien ban WarePro. Can $ExpectedVersion, nhan $actualVersion."
    }

    if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
        throw "Khong tim thay cau hinh tai $settingsPath."
    }

    $settingsText = Get-Content -LiteralPath $settingsPath -Raw
    $settings = $settingsText | ConvertFrom-Json
    if ($settings.schemaVersion -ne 1) {
        throw "schemaVersion cau hinh khong duoc ho tro: $($settings.schemaVersion)."
    }

    if ($Mode -eq 'AppOnly' -and $settings.database.authentication -ne $Authentication) {
        throw "Sai kieu dang nhap database. Can $Authentication, nhan $($settings.database.authentication)."
    }

    if ($settingsText -match '(?i)(Password|Pwd)\s*[:=]') {
        throw 'warepro.settings.json chua truong mat khau; credential phai nam trong Windows Credential Manager.'
    }

    if (Test-Path -LiteralPath (Join-Path $applicationDirectory 'warepro.settings.json')) {
        throw 'Cau hinh dang nam trong Program Files thay vi ProgramData.'
    }

    $shortcuts = @(
        Get-ChildItem -Path (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs') -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue |
            Where-Object Name -Like 'WarePro*'
    )
    if ($shortcuts.Count -eq 0) {
        throw 'Khong tim thay shortcut WarePro trong Start Menu.'
    }

    [System.IO.Directory]::CreateDirectory($localDataDirectory) | Out-Null
    $writeProbe = Join-Path $localDataDirectory 'installer-smoke-write.probe'
    [System.IO.File]::WriteAllText($writeProbe, 'ok')
    Remove-Item -LiteralPath $writeProbe -Force

    if ((Test-Path -LiteralPath $installerLog) -and
        (Select-String -LiteralPath $installerLog -Pattern '(?i)(Password|Pwd)\s*=' -Quiet)) {
        throw 'Installer log chua password.'
    }

    return [ordered]@{
        applicationPath = $applicationPath
        actualVersion = $actualVersion
        settingsPath = $settingsPath
        settingsSha256 = Get-FileSha256 -Path $settingsPath
        shortcutCount = $shortcuts.Count
        localDataWritable = $true
        authentication = $settings.database.authentication
    }
}

function Save-WareProSqlCredential {
    param([Parameter(Mandatory = $true)][System.Management.Automation.PSCredential]$Credential)

    if (-not ('WareProSmokeCredential' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class WareProSmokeCredential
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredWrite(ref NativeCredential credential, uint flags);
}
'@
    }

    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToCoTaskMemUnicode($Credential.Password)
    try {
        $native = New-Object 'WareProSmokeCredential+NativeCredential'
        $native.Type = 1
        $native.TargetName = 'WarePro/Database'
        $native.CredentialBlobSize = $Credential.Password.Length * 2
        $native.CredentialBlob = $passwordPointer
        $native.Persist = 2
        $native.UserName = $Credential.UserName
        if (-not [WareProSmokeCredential]::CredWrite([ref]$native, 0)) {
            throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeCoTaskMemUnicode($passwordPointer)
    }
}

function New-ProtectedBootstrapSecretFile {
    param([Parameter(Mandatory = $true)][System.Management.Automation.PSCredential]$Credential)

    $path = Join-Path ([System.IO.Path]::GetTempPath()) ("warepro-bootstrap-{0}.secret" -f [Guid]::NewGuid().ToString('N'))
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToCoTaskMemUnicode($Credential.Password)
    try {
        $password = [Runtime.InteropServices.Marshal]::PtrToStringUni($pointer)
        [System.IO.File]::WriteAllText($path, $password, [Text.UTF8Encoding]::new($false))
        $password = $null
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeCoTaskMemUnicode($pointer)
    }

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    & "$env:SystemRoot\System32\icacls.exe" $path '/inheritance:r' '/grant:r' '*S-1-5-18:F' '*S-1-5-32-544:F' "${identity}:F" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        throw 'Khong the bao ve file bootstrap secret.'
    }
    return $path
}
function Assert-PendingFullInstallResume {
    param([Parameter(Mandatory = $true)][int]$InstallExitCode)

    $pendingNow = 1 -eq (Get-ItemProperty -LiteralPath $pendingInstallerRegistryPath -Name Pending -ErrorAction SilentlyContinue).Pending
    if ($InstallExitCode -eq 3010) {
        if (-not $pendingNow) {
            throw 'SQL restart did not persist PendingFullInstall state.'
        }
        return [ordered]@{
            status = 'RestartRequiredRerunInstaller'
            databaseConnection = 'NotVerified'
        }
    }

    if ($pendingBefore) {
        if ($pendingNow) {
            throw 'PendingFullInstall was not cleared after resumed finalize.'
        }
        if (-not (Test-Path -LiteralPath $machineSetupHelperLog -PathType Leaf)) {
            throw "Missing setup helper log: $machineSetupHelperLog"
        }
        $helperLog = Get-Content -LiteralPath $machineSetupHelperLog -Raw
        $prepare = $helperLog.LastIndexOf('command=prepare-database exit=0', [StringComparison]::Ordinal)
        $finalize = $helperLog.LastIndexOf('command=finalize-database exit=0', [StringComparison]::Ordinal)
        if ($prepare -lt 0 -or $finalize -le $prepare) {
            throw 'Resume did not prove prepare-database then finalize-database exit=0.'
        }
    }

    return $null
}
function Assert-ApplicationAndDatabase {
    param([Parameter(Mandatory = $true)][int]$InstallExitCode)

    $resumeState = Assert-PendingFullInstallResume -InstallExitCode $InstallExitCode
    if ($null -ne $resumeState) {
        return $resumeState
    }

    $applicationProcess = Start-Process -FilePath $applicationPath -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    try {
        do {
            Start-Sleep -Seconds 1
            $applicationProcess.Refresh()
            if ($applicationProcess.HasExited) {
                throw "WarePro da thoat khi khoi dong, exit code $($applicationProcess.ExitCode)."
            }
        }
        while ($applicationProcess.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)

        if ($applicationProcess.MainWindowHandle -eq [IntPtr]::Zero) {
            throw 'WarePro khong mo duoc cua so trong 45 giay.'
        }
    }
    finally {
        if (-not $applicationProcess.HasExited) {
            [void]$applicationProcess.CloseMainWindow()
            if (-not $applicationProcess.WaitForExit(5000)) {
                Stop-Process -Id $applicationProcess.Id -Force
                $applicationProcess.WaitForExit()
            }
        }
    }

    if (-not (Test-Path -LiteralPath $setupHelperPath -PathType Leaf)) {
        throw "Khong tim thay WarePro.SetupHelper.exe tai $setupHelperPath."
    }

    $helperArguments = @(
        'test-connection',
        '--config',
        "`"$settingsPath`"",
        '--mode',
        'app-only',
        '--log',
        "`"$setupHelperLog`""
    )
    $helperProcess = Start-Process -FilePath $setupHelperPath -ArgumentList $helperArguments -WindowStyle Hidden -Wait -PassThru
    if ($helperProcess.ExitCode -ne 0) {
        throw "SetupHelper khong ket noi duoc database, exit code $($helperProcess.ExitCode)."
    }

    return [ordered]@{
        status = 'Started'
        mainWindowCreated = $true
        databaseConnection = 'Passed'
        setupHelperLog = $setupHelperLog
    }
}

$signatureEvidence = Assert-InstallerSignature
$sqlBefore = Get-SqlServiceEvidence
$settingsHashBefore = if (Test-Path -LiteralPath $settingsPath) { Get-FileSha256 -Path $settingsPath } else { $null }
$exitCode = $null
$installedState = $null
$runtimeProbe = $null
$errorMessage = $null

try {
    switch ($Mode) {
        'Full' {
            if ($null -eq $BootstrapAdminCredential) {
                throw 'Full silent smoke can tham so -BootstrapAdminCredential.'
            }
            $bootstrapSecretPath = New-ProtectedBootstrapSecretFile -Credential $BootstrapAdminCredential
            try {
                $arguments = @(
                    '/VERYSILENT',
                    '/SUPPRESSMSGBOXES',
                    '/NORESTART',
                    '/RESTARTEXITCODE=3010',
                    '/TYPE=full',
                    "/WAREPROBOOTSTRAPSECRETFILE=`"$bootstrapSecretPath`"",
                    "/LOG=`"$installerLog`""
                )
                $exitCode = Invoke-ProcessAndCheckExitCode -FilePath $resolvedInstaller -Arguments $arguments
            }
            finally {
                Remove-Item -LiteralPath $bootstrapSecretPath -Force -ErrorAction SilentlyContinue
            }
            $installedState = Assert-InstalledState
        }
        'AppOnly' {
            if ($Authentication -eq 'SqlPassword') {
                if ($null -eq $SqlCredential) {
                    throw 'AppOnly voi SqlPassword can tham so -SqlCredential.'
                }
                Save-WareProSqlCredential -Credential $SqlCredential
            }
            $arguments = @(
                '/VERYSILENT',
                '/SUPPRESSMSGBOXES',
                '/NORESTART',
                '/RESTARTEXITCODE=3010',
                '/TYPE=app-only',
                "/WAREPROSERVER=`"$SqlServer`"",
                "/WAREPRODATABASE=`"$Database`"",
                "/WAREPROAUTH=$Authentication",
                "/LOG=`"$installerLog`""
            )
            $exitCode = Invoke-ProcessAndCheckExitCode -FilePath $resolvedInstaller -Arguments $arguments
            $installedState = Assert-InstalledState
        }
        'Upgrade' {
            if (-not (Test-Path -LiteralPath $applicationPath)) {
                throw 'Upgrade can mot phien ban WarePro cu da cai san.'
            }

            $arguments = @(
                '/VERYSILENT',
                '/SUPPRESSMSGBOXES',
                '/CLOSEAPPLICATIONS',
                '/NORESTART',
                '/RESTARTEXITCODE=3010',
                '/TYPE=app-only',
                '/WAREPROMODE=upgrade',
                "/LOG=`"$installerLog`""
            )
            $exitCode = Invoke-ProcessAndCheckExitCode -FilePath $resolvedInstaller -Arguments $arguments
            $installedState = Assert-InstalledState
            if ($settingsHashBefore -and $installedState.settingsSha256 -ne $settingsHashBefore) {
                throw 'Upgrade da thay doi cau hinh may dang dung.'
            }
        }
        'Uninstall' {
            $uninstallerPath = Join-Path $applicationDirectory 'unins000.exe'
            if (-not (Test-Path -LiteralPath $uninstallerPath -PathType Leaf)) {
                throw "Khong tim thay Uninstall tai $uninstallerPath."
            }

            $arguments = @(
                '/VERYSILENT',
                '/SUPPRESSMSGBOXES',
                '/NORESTART',
                '/RESTARTEXITCODE=3010',
                "/LOG=`"$installerLog`""
            )
            $exitCode = Invoke-ProcessAndCheckExitCode -FilePath $uninstallerPath -Arguments $arguments
            if (Test-Path -LiteralPath $applicationPath) {
                throw 'WarePro.exe van con sau uninstall.'
            }
            if ($settingsHashBefore -and -not (Test-Path -LiteralPath $settingsPath)) {
                throw 'Uninstall da xoa cau hinh mac dinh can duoc giu lai.'
            }
        }
    }

    if ($null -ne $installedState) {
        $runtimeProbe = Assert-ApplicationAndDatabase -InstallExitCode $exitCode
    }
}
catch {
    $errorMessage = $_.Exception.Message
    throw
}
finally {
    $sqlAfter = Get-SqlServiceEvidence
    if ($sqlBefore.exists -and -not $sqlAfter.exists) {
        $errorMessage = 'SQL Server Express khong con sau smoke test.'
    }

    $evidence = [ordered]@{
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
        machine = $env:COMPUTERNAME
        mode = $Mode
        installerPath = $resolvedInstaller
        installerSha256 = Get-FileSha256 -Path $resolvedInstaller
        signature = $signatureEvidence
        expectedVersion = $ExpectedVersion
        sqlServer = $SqlServer
        database = $Database
        sqlBefore = $sqlBefore
        sqlAfter = $sqlAfter
        installerLog = $installerLog
        exitCode = $exitCode
        installedState = $installedState
        runtimeProbe = $runtimeProbe
        error = $errorMessage
    }
    $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding UTF8

    if ($sqlBefore.exists -and -not $sqlAfter.exists) {
        throw $errorMessage
    }
}

if ($exitCode -eq 3010) {
    Write-Warning "Smoke phase 1 requires restart and rerun of the same installer. Evidence: $evidencePath"
}
else {
    Write-Host "Smoke $Mode dat. Evidence: $evidencePath"
}
