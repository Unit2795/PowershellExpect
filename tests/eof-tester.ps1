# Unload the release version of PowershellExpect so it doesn't interfere with the development version
Remove-Module -Name "PowershellExpect"

# Import the development version of PowershellExpect
$module = Join-Path $PSScriptRoot "../PowershellExpect/PowershellExpect.psm1"
Import-Module $module

Spawn -Command "pwsh.exe -File ./eof-simulator.ps1" -WorkDir "C:\Repositories\PowershellExpect\tests" -Timeout 10 -ShowTerminal
$eof = Expect -EOF

$info = SpawnInfo

Write-Host "Test completed successfully!"
Write-Host $info
Write-Host $eof.ExitCode