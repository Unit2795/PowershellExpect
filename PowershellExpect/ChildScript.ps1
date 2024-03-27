param(
    [string]$sessionId
)

Write-Host "Session ID: $sessionId"

function global:prompt {
    # Store the exit code of the last command
    $lastExitCode = $LASTEXITCODE
    
    $isNull = $lastExitCode -eq $null
    
    # Execute any custom actions here
    # For example, checking the exit code and displaying a message if successful
    if ($lastExitCode -ne $null) {
        Write-Host "EXPECTEOF`e[9D" -NoNewline
    }

    return "PS $PWD> "
}