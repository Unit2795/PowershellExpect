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

$process = Spawn {
    sleep 5
    Send "node -v"
    #Expect "20"
    sleep 2
    Send "npm -v"
    #Expect "10.*"
    sleep 2
    Send "pnpm -v"          
    sleep 2
    #Expect "8\..*"
} -Timeout 5 -EnableLogging

$process = Spawn {
    Send "cd C:\Users\david\OneDrive\Desktop\testdir"
    Send "Remove-Item -Path 'C:\Users\david\OneDrive\Desktop\testdir\*' -Recurse -Force"
    Send "pnpm create tauri-app"
    sleep 2
    #Expect "Project name"
    Send "."
    sleep 2
    #Expect "Package"
    Send "hello-world"
    sleep 2
    #Expect "Choose which language"
    Send "`e[A"
    sleep 2
} $process -Timeout 5 -EnableLogging

Spawn {
    #Expect "Choose your UI"
    Send "`e[A"
    Sleep 5
    #Expect -EOF
} $process -EnableLogging -Timeout 5