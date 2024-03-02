# Import primary driver DLL
$driverDLLPath = Join-Path $PSScriptRoot "/PowershellExpectDriver.dll"
Add-Type -Path $driverDLLPath

# Import helper functions
$helpersPath = Join-Path $PSScriptRoot "Helpers.ps1"
. $helpersPath

function Spawn {
    param(
        [ScriptBlock]$ScriptBlock,
        $Process,
        [int]$Timeout = 0,
        [switch]$EnableLogging = $false,
        [switch]$ShowTerminal = $false
    )

    if ($null -eq $Process)
    {
        # Initialize a new instance of the C# driver object
        $pty = New-Object PowershellExpectDriver.Driver

        $pty.Spawn($PWD, $Timeout, $EnableLogging, $ShowTerminal, $driverDLLPath) | Out-Null    

        $driver = $pty
    }
    else 
    {
        $driver = $Process
    }

    $ExecutionContext.InvokeCommand.InvokeScript($true, $ScriptBlock, $null, $driver) | Out-Null

    if (Test-OutputCaptured) {
        return $driver
    }
}

function Send {
    param(
        [string]$Command,
        [switch]$NoNewline = $false,
        [int]$IdleDuration = 0,
        [int]$IgnoreLines = 0
    )

    try
    {
        $result = $driver.Send($Command, $NoNewline, $IdleDuration, $IgnoreLines)
        
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
        $result = $driver.Expect($Regex, $Timeout, $ContinueOnTimeout, $EOF)
        
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
    
    $driver.ShowTerminal()
}

function HideTerminal {
    $driver.HideTerminal()
}

Export-ModuleMember -Function Spawn, Send, Expect, ShowTerminal, HideTerminal