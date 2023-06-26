Add-Type -Path  "$PSScriptRoot/PowershellExpect.cs"
$processStarter = New-Object ProcessStarter

function Spawn {
    param(
        [string]$Command = $null,
        [int]$Timeout = $null
    )
    try
    {
        $processStarter.StartProcess($Timeout)

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

function Send {
    param(
        [string]$Command,
        [switch]$NoNewline = $false
    )
    try
    {
        $processStarter.Send($Command, $NoNewline)
    } catch {
        Write-Warning "PowershellExpect encountered an error!"
        Write-Error $_
        throw
    }
}

function Expect {
    param(
        [string]$Regex,
        [int]$Timeout = $null,
        [switch]$ContinueOnTimeout,
        [switch]$EOF
    )
     try
     {
         $processStarter.Expect($Regex, $Timeout, $ContinueOnTimeout, $EOF)
     } catch {
         Write-Warning "PowershellExpect encountered an error!"
         Write-Error $_
         throw
     }
}

Export-ModuleMember -Function Spawn, Expect, Send