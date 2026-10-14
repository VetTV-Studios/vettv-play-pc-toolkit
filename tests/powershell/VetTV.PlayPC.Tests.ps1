BeforeAll { Import-Module "$PSScriptRoot/../../src/VetTV.PlayPC.PowerShell/VetTV.PlayPC.psd1" -Force }
Describe 'Get-VetTVPlayPCToolkitInfo' {
    It 'returns the product name' { (Get-VetTVPlayPCToolkitInfo).Name | Should -Be 'VetTV Play PC Toolkit' }
}
