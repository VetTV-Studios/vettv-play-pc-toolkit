# VetTV Play PC Toolkit PowerShell module. Checks arrive in M1.
function Get-VetTVPlayPCToolkitInfo {
    [CmdletBinding()] param()
    [pscustomobject]@{ Name = 'VetTV Play PC Toolkit'; Disclaimer = 'Not affiliated with Google.' }
}
Export-ModuleMember -Function Get-VetTVPlayPCToolkitInfo
