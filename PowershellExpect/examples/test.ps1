<#
#       Note that this example is not good form, it's generally not recommended to spin up this many individual
#    processes for such a simple script, in fact this library is not necessary for such a simple usecase. 
#    Nonetheless, it's satisfactory for the purposes of checking the functionality of the library with live tools.
#
#    This script requires you to have node, npm, and pnpm installed, adjust as necessary.
#>

Import-Module "..\PowershellExpect.psm1"

# Check Node Version
$nodeProcess = Spawn -Timeout 5 -EnableLogging
$nodeProcess.Send("node -v")
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
}