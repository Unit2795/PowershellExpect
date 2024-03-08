# Unload the release version of PowershellExpect so it doesn't interfere with the development version
Remove-Module -Name "PowershellExpect"

# Import the development version of PowershellExpect
$module = Join-Path $PSScriptRoot "../PowershellExpect/PowershellExpect.psm1"
Import-Module $module


Spawn -Command "wsl ~ -d Ubuntu-22.04" -Timeout 5 -EnableLogging
ShowTerminal
Send "uname -a"
Expect "microsoft-standard-WSL2"
Send "lscpu"
Expect "Architecture:.*x86_64"
Expect "Byte Order:.*Little Endian"
$data = SpawnInfo


Write-Host "Test completed successfully!"
Write-Host $data