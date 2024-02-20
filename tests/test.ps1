<#
#       Note that this example is not good form, it's generally not recommended to spin up this many individual
#    processes for such a simple script, in fact this library is not necessary for such a simple usecase. 
#    Nonetheless, it's satisfactory for the purposes of checking the functionality of the library with live tools.
#
#    This script requires you to have node, npm, and pnpm installed, adjust as necessary.
#>

# Unload the release version of PowershellExpect so it doesn't interfere with the development version
Remove-Module -Name "PowershellExpect"

# Import the development version of PowershellExpect
$module = Join-Path $PSScriptRoot "../PowershellExpect/PowershellExpect.psm1"
Import-Module $module

# Check Node Version
$nodeProcess = Spawn -Timeout 5 -EnableLogging
$nodeProcess.Send("cd C:\Users\david\OneDrive\Desktop\testdir")
$nodeProcess.Send("pnpm create tauri-app")
$nodeProcess.Expect("Project name 123", @{timeout = 10})
$nodeProcess.Send(".")
$nodeProcess.Expect("Package")
$nodeProcess.Send("hello-world")
<#$nodeProcess.Expect("Current directory directory is not empty")
$nodeProcess.Send("y", $true)#>
$nodeProcess.Expect("Choose which language")
$nodeProcess.Send($arrowDown)
$nodeProcess.Exit();
Write-Host "Finished"

#Write-Host "Value: $val"
<#$nodeProcess.Send("\u001B[D", $false)#>
<#
$node = $nodeProcess.Expect("v18.*")
$nodeProcess.Exit()

# Check NPM version
$npmProcess = Spawn -Timeout 5 -EnableLogging
$npmProcess.Send("npm -v")
$npm = $npmProcess.Expect("10.*")
$npmProcess.Exit()

# Check PNPM version
$pnpmProcess = Spawn -EnableLogging
$pnpm = $pnpmProcess.SendAndWait("pnpm -v", 2)
$pnpmProcess.Exit()

Write-Host "Node Version: $node Node"
Write-Host "NPM Version: $npm NPM"
Write-Host "PNPM Version: $pnpm PNPM"

if ($node -match "v18.18.0")
{
    Write-Host "Node Version is Good (18.18.0)"
}
else
{
    Write-Host "Node version must be 18.18.0"
}#>
