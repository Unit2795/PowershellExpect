$driverDLL = Join-Path $PSScriptRoot "PowershellExpectDriver.dll"
Add-Type -Path $driverDLL

<# 
#   Spawn a child PowerShell process to execute commands in.
#   Returns an object containing the functions you may execute against the spawned PowerShell process.
#>
function Spawn {
    param(
        # Timeout in seconds
        [int]$Timeout = $null,
        [switch]$EnableLogging = $false
    )
    try
    {
        # Initialize a new instance of the C# driver object
        $driver = New-Object PowershellExpectDriver.PTYDriver
        
        # Start the process
        $pty = $driver.StartProcess($PWD, $Timeout, $EnableLogging)

        # Store the ProcessHandler instance in the process object to ensure that the spawned process is persisted
        $pty | Add-Member -MemberType NoteProperty -Name "PTYDriver" -Value $driver
        
        # START COMMANDS
        # Attach commands to the object
        $pty | Add-Member -MemberType ScriptMethod -Name "Send" -Value {
            param(
                [string]$CommandToSend,
                [switch]$NoNewline = $false
            )
            $this.PTYDriver.Send($CommandToSend, $NoNewline)
        }
        $pty | Add-Member -MemberType ScriptMethod -Name "SendAndWait" -Value {
            param(
                [string]$Command,
                [int]$IgnoreLines = 0,
                [int]$WaitForIdle = 3,
                [switch]$NoNewline = $false
            )
            try
            {
                $this.ProcessHandler.SendAndWait($Command, $IgnoreLines, $WaitForIdle, $NoNewline)
            } catch {
                Write-Warning "PowershellExpect encountered an error!"
                Write-Error $_
                throw
            }
        }
        $pty | Add-Member -MemberType ScriptMethod -Name "Expect" -Value {
            param(
                [string]$Regex,
                [int]$Timeout = $null,
                [switch]$ContinueOnTimeout
            )
            try
            {
                return $this.ProcessHandler.Expect($Regex, $Timeout, $ContinueOnTimeout)
            } catch {
                Write-Warning "PowershellExpect encountered an error!"
                Write-Error $_
                throw
            }
        }
        $pty | Add-Member -MemberType ScriptMethod -Name "Exit" -Value {
            try
            {
                return $this.ProcessHandler.Exit()
            } catch {
                Write-Warning "PowershellExpect encountered an error!"
                Write-Error $_
                throw
            }
        }
        # END COMMANDS

        return $pty
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
    }
}

Export-ModuleMember -Function Spawn