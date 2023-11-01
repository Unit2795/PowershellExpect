Import-Module "..\PowershellExpect.psm1"

$myProcess2 = Spawn -Timeout 5
$myProcess2.Send("npm -v")
$npm = $myProcess2.Expect("10.*")
$myProcess2.Exit()

$myProcess = Spawn -Timeout 5
$myProcess.Send("node -v")
$node = $myProcess.Expect("v18.*")
$myProcess.Exit()

$waitProcess = Spawn
$pnpm = $waitProcess.SendAndWait("pnpm -v", 2)
$waitProcess.Exit()

Write-Host "Node Version: $node PNPM"
Write-Host "NPM Version: $npm PNPM"
Write-Host "PNPM Version: $pnpm PNPM"

if ($node -match "v18.18.0")
{
    Write-Host "Node Version is Good"
}
else
{
    Write-Host "Node version must be 18.18.0"
}

<#SendKeys "n","o","d","e","Spacebar","Subtract","v","Enter" -Simultaneous
Expect "v18.*"
Expect -EOF#>

<#
# Execute the command and store the result in a variable
$nodeVersion = & node -v

# Display the result to the console
Write-Host "Node.js Version: $nodeVersion"
#>

<#
Spawn
$node = SendAndWaitForIdle "node -v" -IgnoreLines 2 -WaitForIdle 10
$npm = SendAndWaitForIdle "npm -v" -IgnoreLines 1 -WaitForIdle 10
Expect -EOF

Write-Host "Node Version: $node"
Write-Host "NPM Version: $npm"#>
