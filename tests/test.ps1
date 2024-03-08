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

$process1 = Spawn -Command "pwsh.exe" -Timeout 5 -EnableLogging
    ShowTerminal -Interactive
    Sleep 10
    Send "node -v"
    Expect "v20"
    Send "npm -v"
    Expect "10.*"
    Sleep 5
    ShowTerminal
    Sleep 5
    Send "pnpm -v"
    Expect "8\..*"

$process2 = Spawn -Timeout 20 -EnableLogging
    ShowTerminal -Interactive
    Send "cd C:\Users\david\OneDrive\Desktop\testdir"
    Send "Remove-Item -Path 'C:\Users\david\OneDrive\Desktop\testdir\*' -Recurse -Force"
    Send "pnpm create tauri-app"
    Expect "Project name"
    Send "."
    Expect "Package"
    Send "hello-world"
    Expect "Choose which language"
    Send "`e[A"

Despawn $process1

Spawn $process2 -EnableLogging -Timeout 5
    Expect "Choose your UI"
    Send "`e[A"
    Sleep 5

Despawn