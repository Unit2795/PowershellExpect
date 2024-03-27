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

<#$process1 = Spawn -Command "pwsh.exe" -Timeout 5 -EnableLogging
ShowTerminal -Interactive
Send "node -v"
Expect "v20"
Send "npm -v"
Expect "10.*"
ShowTerminal
Send "pnpm -v"
Expect "8\..*"#>

$process2 = Spawn -Timeout 20 -EnableLogging -X 120 -Y 9001
Send '$host.UI.RawUI.WindowSize'
Send '$host.UI.RawUI.BufferSize'
Send "cd C:\Users\david\OneDrive\Desktop\testdir"
Send "Remove-Item -Path 'C:\Users\david\OneDrive\Desktop\testdir\*' -Recurse -Force"
Send "pnpm create tauri-app"
Expect "Project name"
Send "."
Expect "Package"
ShowTerminal -Interactive
Sleep 10
Send "`e[2J" -NoNewline
Expect "Choose which language"
Send "`e[A"

#Despawn $process1

Spawn $process2 -EnableLogging -Timeout 5 -X 120 -Y 9001
Expect "Choose your UI"
Send "`e[A"
HideTerminal
Sleep 5
Send '$host.UI.RawUI.WindowSize'
Send '$host.UI.RawUI.BufferSize'
Send "node -v"
Expect "v20"
Send "npm -v"
Expect "10.*"
ShowTerminal -Interactive
Sleep 5
Send "pnpm -v"
Send '$host.UI.RawUI.WindowSize'
Send '$host.UI.RawUI.BufferSize'
Sleep 30

Despawn