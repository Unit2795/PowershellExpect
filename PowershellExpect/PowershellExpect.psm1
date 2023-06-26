Add-Type -Path  "$PSScriptRoot/PowershellExpect.cs"
$processHandler = New-Object PowershellExpectHandler

# Spawn a child process to execute commands in
function Spawn {
    param(
        # Optional command to run with the spawn (otherwise will just start a powershell process)
        [string]$Command = $null,
        # Timeout in seconds
        [int]$Timeout = $null
    )
    try
    {
        $processHandler.StartProcess($Timeout)

        if ($Command)
        {
            Send($Command)
        }
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
    }
}

# Send a command to the spawned child process
function Send {
    param(
        [string]$Command,
        # Optionally disable sending the newline character, which submits the response (you can still provide manually with \n)
        [switch]$NoNewline = $false
    )
    try
    {
        $processHandler.Send($Command, $NoNewline)
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
    }
}

# Wait for a regular expression match to be detected in the standard output of the child process
function Expect {
    param(
        [string]$Regex,
        [int]$Timeout = $null,
        [switch]$ContinueOnTimeout,
        [switch]$EOF
    )
     try
     {
         $processHandler.Expect($Regex, $Timeout, $ContinueOnTimeout, $EOF)
     } catch {
         Write-Warning "PowershellExpect encountered an error!"
         Write-Error $_
         throw
     }
}

Export-ModuleMember -Function Spawn, Expect, Send