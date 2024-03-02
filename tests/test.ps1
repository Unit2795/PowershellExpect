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

<#Spawn {
    Send "C:\Repositories\PowershellExpect\tests\keytest.ps1"
} -ShowTerminal#>

$process = Spawn {
    sleep 5
    Send "node -v"
    Expect "20"
    Send "npm -v"
    Expect "10.*"
    Send "pnpm -v"
    Expect "8\..*"
} -Timeout 5 -EnableLogging

$process = Spawn {
    Send "cd C:\Users\david\OneDrive\Desktop\testdir"
    Send "Remove-Item -Path 'C:\Users\david\OneDrive\Desktop\testdir\*' -Recurse -Force"
    Send "pnpm create tauri-app"
    Expect "Project name"
    Send "."
    Expect "Package"
    Send "hello-world"
    Expect "Choose which language"
    Send "`e[A"
} $process -Timeout 5 -EnableLogging

Spawn {
    Expect "Choose your UI"
    Send "`e[A"
    Sleep 5
    Expect -EOF
} $process -EnableLogging -Timeout 5
