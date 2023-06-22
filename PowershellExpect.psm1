$process = New-Object System.Diagnostics.Process
$processInfo = New-Object System.Diagnostics.ProcessStartInfo
$spawned = $false
$powershell7Exe = "pwsh.exe"

Register-ObjectEvent -InputObject $process -EventName Exited -Action {
    Despawn
}

function InfoMessage {
    param (
        [string] $Message
    )
    Write-Host "[PowerShellExpect] LOG: $Message" -ForegroundColor Cyan
}

# Throw an error with a custom message.
function ThrowError {
    param (
        [string] $Message
    )
    throw "[PowerShellExpect] ERROR: $Message"
}

function AsyncSpawn {
    param (
        [ScriptBlock] $ScriptBlock,
        [string] $Command = $powershell7Exe
    )

    # Start the script block as a job
    Start-Job -ScriptBlock {
        # Load the module in the job's session
        Import-Module -Name MyModule

        # Call the Spawn function
        Spawn -ScriptBlock $ScriptBlock -Command $Command
    }
}

function Spawn {
    param (
        [ScriptBlock] $ScriptBlock,
        # We'll try to use pwsh.exe by default (Powershell 7)
        [string] $Command = $powershell7Exe
    )

    # If pwsh.exe is not installed, we'll use powershell.exe (Powershell 5)
    if ( ($Command -eq $powershell7Exe) -and ((Get-Command -ErrorAction Ignore $Command) -eq $null) ) {
        $Command = "powershell.exe"
    }
    # If process is not found, throw an error
    if ((Get-Command -ErrorAction Ignore $Command) -eq $null) {
        ThrowError "Spawn could not find '$Command', you may need to manually provide the path to your executable to the Spawn command"
    }

    InfoMessage "Attempting to spawn: $Command"

    # Set the process target to the provided executable and redirect input/output to the current process.
    $processInfo.FileName = $Command
    $processInfo.RedirectStandardError = $true
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardInput = $true
    # Instead of launching a new window, we'll use the current one and watch its output and redirect input
    $processInfo.UseShellExecute = $false

    # Start the process
    try
    {
        $spawned = $true
        $process.StartInfo = $processInfo
        $process.EnableRaisingEvents = $true
        $process.Start() | Out-Null

        # Call the passed scriptblock
        . $ScriptBlock

        # Destroy the process
        Despawn
    } catch {
        ThrowError "Spawn failed to start process '$Command'"
    }
}

function Despawn {
    if ($process) {
        InfoMessage "Despawning process ID: $($process.Id)"
        if (!$process.HasExited) {
            $process.Kill()
        }
        $process.Close()
    }
}

function Send {
    param (
        [string] $Command
    )
    if (!$process) {
        # If a spawn was called, but the process is not found, throw an error, otherwise spawn a new default process
        if ($spawned)
        {
            ThrowError "Send could not find a process to send to, you may need to manually provide the path to your executable to the Spawn command"
        }
        # If no spawn was provided at all, spawn a new default powershell process
        else
        {
            InfoMessage "No Spawn command was provided, starting a new powershell subprocess"
            Spawn
        }
    }

    $process.StandardInput.WriteLine($Command);
}

function Expect {
    param (
        [string[]] $Command
    )
    do {
        $out = $process.StandardOutput.ReadLine()
        Write-Host $out
    } until ($Command | Where-Object {$out -match $_})
}

function ExpectThenSend {
    param (
        [string] $Command,
        [string[]] $Expect
    )
    Expect($Expect);
    Send($Command);
}

Export-ModuleMember -Function Despawn, ClearProcess, Expect, ExpectThenSend, Send, Spawn, AsyncSpawn