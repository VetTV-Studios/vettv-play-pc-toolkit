BeforeAll { Import-Module "$PSScriptRoot/../../src/VetTV.PlayPC.PowerShell/VetTV.PlayPC.psd1" -Force }
Describe 'Get-VetTVPlayPCToolkitInfo' {
    It 'returns the product name' { (Get-VetTVPlayPCToolkitInfo).Name | Should -Be 'VetTV Play PC Toolkit' }
}
Describe 'Get-VetTVPlayPCInstall' {
    It 'reports both products' {
        $r = @(Get-VetTVPlayPCInstall)
        $r.Count | Should -Be 2
        $r.Product | Should -Contain 'DeveloperEmulator'
    }
}
Describe 'Test-VetTVPlayPCVirtualization' {
    It 'returns pass/warn/fail results with a fix hint for non-pass' {
        foreach ($c in Test-VetTVPlayPCVirtualization) {
            $c.Status | Should -BeIn 'Pass','Warn','Fail'
            if ($c.Status -ne 'Pass') { $c.FixHint | Should -Not -BeNullOrEmpty }
        }
    }
}
