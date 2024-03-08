# Import primary driver DLL
$driverDLLPath = Join-Path $PSScriptRoot "/PowershellExpectDriver.dll"
Add-Type -Path $driverDLLPath

# Import helper functions
$helpersPath = Join-Path $PSScriptRoot "Helpers.ps1"
. $helpersPath

$script:activeProcess = $null

function Spawn {
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    param(
        [Parameter(Position = 0)]
        $Process = $null,
        
        [Parameter(ParameterSetName = 'Command')]
        [string]$Command = "pwsh",
        
        [string]$WorkDir = $PWD,
        [int]$Timeout = 0,
        [switch]$EnableLogging = $false,
        [switch]$ShowTerminal = $false
    )
    
    if ($Process -ne $null) {
        $script:activeProcess = $Process
    } else {
        # Initialize a new instance of the C# driver object
        $pty = New-Object PowershellExpectDriver.Driver

        $pty.Spawn($WorkDir, $Timeout, $EnableLogging, $ShowTerminal, $Command) | Out-Null

        $script:activeProcess = $pty
    }
    
    if (Test-OutputCaptured) {
        return $script:activeProcess
    }
}

function Send {
    param(
        [string]$Command,
        [switch]$NoNewline = $false,
        [int]$IdleDuration = 0,
        [int]$IgnoreLines = 0
    )

    try {
        $result = $script:activeProcess.Send($Command, $NoNewline, $IdleDuration, $IgnoreLines)
        
        if ($null -ne $result) {
            $isCaptured = Test-OutputCaptured
            
            if ($isCaptured) {
                return $result
            }
        }
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
        exit
    }
}

function Expect {
    param(
        [string]$Regex,
        [int]$Timeout = 0,
        [switch]$ContinueOnTimeout = $false,
        [switch]$EOF = $false
    )

    try
    {
        $result = $script:activeProcess.Expect($Regex, $Timeout, $ContinueOnTimeout, $EOF)
        
        if ($null -ne $result) {
            $isCaptured = Test-OutputCaptured

            if ($isCaptured) {
                return $result
            }
        }
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
        exit
    }
}

function ShowTerminal {
    param(
        [switch]$Interactive = $false
    )
    $script:activeProcess.ShowTerminal()
}

function HideTerminal {
    $script:activeProcess.HideTerminal()
}

function GetSpawn {
    return $script:activeProcess
}

function SpawnInfo {
    $script:activeProcess.SpawnInfo()
}

function Despawn {
    param(
        $Process
    )
    
    if ($null -eq $Process) {
        $script:activeProcess.Exit()
    } else {
        $Process.Exit()
    }
}

Export-ModuleMember -Function Spawn, Send, Expect, ShowTerminal, HideTerminal, SpawnInfo, GetSpawn, Despawn