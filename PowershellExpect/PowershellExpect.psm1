Add-Type -Path  "$PSScriptRoot/PowershellExpect.cs"

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
        $processHandler = New-Object PowershellExpectHandler
        
        # Start the process
        $process = $processHandler.StartProcess($PWD, $Timeout, $EnableLogging)

        # Store the ProcessHandler instance in the process object to ensure that the spawned process is persisted
        $process | Add-Member -MemberType NoteProperty -Name "ProcessHandler" -Value $processHandler
        
        # START COMMANDS
        # Attach all of the commands to the process object
        $process | Add-Member -MemberType ScriptMethod -Name "Send" -Value {
            param(
                [string]$CommandToSend,
                [switch]$NoNewline = $false
            )
            # Use stored HandlerInstance to send the command
            $this.ProcessHandler.Send($CommandToSend, $NoNewline)
        }
        $process | Add-Member -MemberType ScriptMethod -Name "SendAndWait" -Value {
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
        $process | Add-Member -MemberType ScriptMethod -Name "Expect" -Value {
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
        $process | Add-Member -MemberType ScriptMethod -Name "Exit" -Value {
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

        return $process
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
    }
}

Export-ModuleMember -Function Spawn