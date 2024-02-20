<#
#   This example script is used to validate the automation behavior of the PowerShellExpect library. It uses a
#   simulated remote server (another PowerShell script).
#>

$module = Join-Path $PSScriptRoot "../PowershellExpect/PowershellExpect.psm1"
Import-Module $module

$simulatedServerScriptPath = "./simulator.ps1 -Listen"

# Create a new session with complex_tool
$session = Spawn -EnableLogging -Timeout 10

# Run the server simulator script
$session.Send($simulatedServerScriptPath)

# Connect to the remote server
$session.Send("serveConnect")

# Send the username and password
$session.Send("admin")
$session.Send("secretPass")

# Check for successful login
if ($session.Expect("Welcome to complex_tool")) {
    # List files
    $session.Send("list_files")
    $fileList = $session.Expect("Files: (.*)") # Using regex to capture file list

    # Check for "important.txt" in the output
    if ($fileList -like "*important.txt*") {
        $session.Send("extract_data important.txt")
        $output = $session.Expect("Data: (.*)") # Extracting data from important.txt
    }

    # Check system status
    $session.Send("system_status")
    $status = $session.Expect("Status: (GOOD|FAIR|BAD|CRITICAL)")

    if ($status -match "GOOD|FAIR") {
        Write-Host "System status is acceptable."
    } elseif ($status -match "BAD|CRITICAL") {
        Write-Host "System status is problematic."
        exit 1
    }

    # Logout
    $session.Send("logout")
    if ($session.Expect("Goodbye")) {
        Write-Host "Logged out successfully."
    }

} elseif ($session.Expect("Login Failed")) {
    Write-Host "Login failed!"
    exit 1
}

# Display extracted data if available
if ($output) {
    Write-Host "Extracted data from important.txt: $output"
}

# Close the session
$session.Exit()