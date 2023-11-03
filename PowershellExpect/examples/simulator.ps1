<# 
#   This is a simulator meant to be used with the test2.ps1 script for validating the automation behavior of the
#   PowerShellExpect library.
#>
param(
    [switch]$Listen
)

function Start-Server {
    while ($true) {
        $input = Read-Host
        switch -Regex ($input) {
            '^serveConnect$' {
                Write-Host "Please enter username:"
                $username = Read-Host
                Write-Host "Please enter password:"
                $password = Read-Host
                if ($username -eq 'admin' -and $password -eq 'secretPass') {
                    Write-Host "Welcome to complex_tool"
                } else {
                    Write-Host "Login Failed"
                    break
                }
            }
            '^list_files$' {
                Write-Host "Files: file1.txt, file2.txt, important.txt"
            }
            '^extract_data important\.txt$' {
                Write-Host "Data: This is some important data from the file."
            }
            '^system_status$' {
                # Randomly select a status to simulate real scenarios
                $statuses = @('GOOD', 'FAIR', 'BAD', 'CRITICAL')
                $status = $statuses | Get-Random
                Write-Host "Status: $status"
            }
            '^logout$' {
                Write-Host "Goodbye"
                break
            }
            default {
                Write-Host "Unknown command"
            }
        }
    }
}

if ($Listen) {
    Start-Server
}
