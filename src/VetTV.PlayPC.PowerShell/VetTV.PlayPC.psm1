# VetTV Play PC Toolkit PowerShell module (M1: read-only diagnostics). Not affiliated with Google.

function Get-VetTVPlayPCToolkitInfo {
    [CmdletBinding()] param()
    [pscustomobject]@{ Name = 'VetTV Play PC Toolkit'; Disclaimer = 'Not affiliated with Google.' }
}

function New-VetTVCheck([string]$Category, [string]$Title, [ValidateSet('Pass','Warn','Fail')][string]$Status, [string]$Detail, [string]$FixHint) {
    [pscustomobject]@{ Category = $Category; Title = $Title; Status = $Status; Detail = $Detail; FixHint = $FixHint }
}

function Get-VetTVPlayPCInstall {
    [CmdletBinding()] param()
    $keys = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    $entries = Get-ItemProperty -Path $keys -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like 'Google Play Games*' }
    foreach ($p in 'Consumer', 'DeveloperEmulator') {
        $e = if ($p -eq 'Consumer') { $entries | Where-Object { $_.DisplayName -notlike '*Developer*' } | Select-Object -First 1 }
             else { $entries | Where-Object { $_.DisplayName -like '*Developer Emulator*' } | Select-Object -First 1 }
        $default = if ($p -eq 'Consumer') { Join-Path $env:ProgramFiles 'Google\Play Games' } else { Join-Path $env:ProgramFiles 'Google\Play Games Developer Emulator' }
        $path = if ($e.InstallLocation) { $e.InstallLocation } elseif (Test-Path $default) { $default } else { $null }
        [pscustomobject]@{ Product = $p; Installed = [bool]($e -or $path); Version = $e.DisplayVersion; InstallPath = $path }
    }
}

function Test-VetTVPlayPCVirtualization {
    [CmdletBinding()] param()
    $cpu = Get-CimInstance Win32_Processor -ErrorAction SilentlyContinue | Select-Object -First 1
    $cs  = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
    if ($cs.HypervisorPresent -or $cpu.VirtualizationFirmwareEnabled) { New-VetTVCheck System 'Hardware virtualization' Pass 'VT-x/AMD-V is on.' }
    elseif ($cpu.VirtualizationFirmwareEnabled -eq $false) { New-VetTVCheck System 'Hardware virtualization' Fail 'Disabled in firmware.' 'Enable VT-x / AMD SVM in BIOS/UEFI setup.' }
    else { New-VetTVCheck System 'Hardware virtualization' Warn 'Unknown.' 'Check Task Manager > Performance > CPU.' }
    foreach ($f in 'HypervisorPlatform', 'VirtualMachinePlatform') {
        $s = Get-CimInstance Win32_OptionalFeature -Filter "Name='$f'" -ErrorAction SilentlyContinue
        if ($s.InstallState -eq 1) { New-VetTVCheck System $f Pass 'Enabled.' }
        else { New-VetTVCheck System $f ($(if ($f -eq 'HypervisorPlatform') {'Fail'} else {'Warn'})) 'Not enabled.' "Enable-WindowsOptionalFeature -Online -FeatureName $f (admin), then reboot." }
    }
}

function Get-VetTVPlayPCGpu {
    [CmdletBinding()] param()
    Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch 'Basic Display|Remote Display' } |
        ForEach-Object { [pscustomobject]@{ Name = $_.Name; DriverVersion = $_.DriverVersion; DriverDate = $_.DriverDate } }
}

Export-ModuleMember -Function Get-VetTVPlayPCToolkitInfo, Get-VetTVPlayPCInstall, Test-VetTVPlayPCVirtualization, Get-VetTVPlayPCGpu
