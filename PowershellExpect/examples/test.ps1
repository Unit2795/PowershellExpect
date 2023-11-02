Import-Module "..\PowershellExpect.psm1"

$myProcess2 = Spawn -Timeout 5 -EnableLogging
$myProcess2.Send("npm -v")
$npm = $myProcess2.Expect("10.*")
$myProcess2.Exit()

$myProcess = Spawn -Timeout 5 -EnableLogging
$myProcess.Send("node -v")
$node = $myProcess.Expect("v18.*")
$myProcess.Exit()

$waitProcess = Spawn -EnableLogging
$pnpm = $waitProcess.SendAndWait("pnpm -v", 2)
$waitProcess.Exit()

Write-Host "Node Version: $node PNPM"
Write-Host "NPM Version: $npm PNPM"
Write-Host "PNPM Version: $pnpm PNPM"

if ($node -match "v18.18.0")
{
    Write-Host "Node Version is Good (18.18.0)"
}
else
{
    Write-Host "Node version must be 18.18.0"
}