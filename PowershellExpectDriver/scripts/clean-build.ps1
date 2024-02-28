enum Color {
    Yellow
    Green
    Cyan
    Blue
}

function Color-VT {
    param(
        [Color]$color
    )
    
    switch ($color) {
        ([Color]::Yellow) {
            return "`e[93m"
        }
        ([Color]::Green) {
            return "`e[32m"
        }
        ([Color]::Cyan) {
            return "`e[36m"
        }
        ([Color]::Blue) {
            return "`e[34m"
        }
    }
}

function Info-Message {
    param(
        [string]$message,
        [Color]$color
    )
    $colorVT = Color-VT -color $color
    $boldVT = "`e[1m"
    $resetVT = "`e[0m"
    
    Write-Host "`n$boldVT$colorVT[INFO] $message [INFO]$resetVT`n"
}

Info-Message "Beginning cleaning and build process" Yellow

# Stop all powershell processes except the current one
# Ensures any running instances of the DLL are freed so they may be deleted
Get-Process pwsh | Where-Object { $_.Id -ne $pid } | Stop-Process
Info-Message "Stopped powershell processes" Green

dotnet clean --configuration Release
Info-Message "Cleaning Complete" Cyan

Remove-Item -Path "../PowershellExpect/PowershellExpectDriver.dll" -Force
Info-Message "Deleted PowershellExpect driver DLL" Green

dotnet publish --no-restore --configuration Release
Info-Message "Rebuild Complete" Blue

Info-Message "Cleaning and build complete" Yellow