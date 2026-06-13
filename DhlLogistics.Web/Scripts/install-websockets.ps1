# Elastic Beanstalk Windows deployment-manifest hook (preInstall / postInstall).
# PRIMARY installer for the IIS WebSocket Protocol feature so the Blazor Server
# circuit uses WebSockets instead of falling back to Long Polling.
# Idempotent. Compatible with IIS 10 on Windows Server Core 2025.
#
# IMPORTANT: EB runs deployment scripts under 32-bit PowerShell (SysWOW64). The
# ServerManager module (Get-WindowsFeature / Install-WindowsFeature) does NOT exist
# in 32-bit PowerShell, so the first run must RELAUNCH this script in 64-bit
# PowerShell via the Sysnative alias before touching any ServerManager cmdlet.
if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) {
    $ps64 = Join-Path $env:WINDIR 'Sysnative\WindowsPowerShell\v1.0\powershell.exe'
    if (Test-Path $ps64) {
        & $ps64 -ExecutionPolicy Bypass -NoProfile -File $PSCommandPath
        exit $LASTEXITCODE
    }
}

$ErrorActionPreference = 'Continue'
$log = 'C:\dhl-scripts\install-websockets.log'
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
function Log($m) { "$(Get-Date -Format o)  [is64=$([Environment]::Is64BitProcess)]  $m" | Tee-Object -FilePath $log -Append }

Log "=== manifest hook: checking Web-WebSockets ==="
$f = Get-WindowsFeature -Name Web-WebSockets -ErrorAction SilentlyContinue
if (-not $f -or -not $f.Installed) {
    Log "installing Web-WebSockets ..."
    Install-WindowsFeature -Name Web-WebSockets | Out-Null
    try { & iisreset /restart | Out-Null; Log "iisreset done" }
    catch { Log "iisreset skipped: $($_.Exception.Message)" }
    Log "install complete"
} else {
    Log "Web-WebSockets already installed (idempotent no-op)"
}

# HTTP-reachable proof so the install can be verified WITHOUT RDP/SSM:
# drops ws-status.txt into the live site's wwwroot -> https://pvgt.co.in/ws-status.txt
try {
    Import-Module WebAdministration -ErrorAction Stop
    $phys = (Get-Website -Name 'Default Web Site').physicalPath
    if ($phys) {
        $phys    = [System.Environment]::ExpandEnvironmentVariables($phys)
        $wwwroot = Join-Path $phys 'wwwroot'
        if (Test-Path $wwwroot) {
            $state = (Get-WindowsFeature Web-WebSockets).Installed
            "Web-WebSockets Installed=$state at=$(Get-Date -Format o)" |
                Out-File (Join-Path $wwwroot 'ws-status.txt') -Encoding ascii -Force
            Log "wrote ws-status.txt (Installed=$state)"
        } else { Log "wwwroot not found at $wwwroot; marker skipped" }
    }
} catch { Log "marker step skipped: $($_.Exception.Message)" }
exit 0
