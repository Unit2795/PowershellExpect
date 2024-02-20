function Test-OutputCaptured {
    # Get the call context of the function that called this helper function
    $callerInvocationLine = (Get-PSCallStack)[1].InvocationInfo.Line

    # Check if the line contains variable assignment syntax before calling a function
    if ($callerInvocationLine -match "^\s*\$\w+\s*=") {
        return $true
    } else {
        return $false
    }
}

function Get-SilentValue {
    param (
        [Parameter(Mandatory=$true)]
        $Value
    )

    # Use the helper function to determine if output is being captured
    $isCaptured = Test-OutputCaptured

    if ($isCaptured) {
        # If output is being captured, return the value
        return $Value
    } else {
        # Otherwise, suppress output
        # Nothing to do here if we're not returning anything
    }
}

# Example usage:
$result = Get-SilentValue 10  # This should capture the value into $result without printing it
$result  # This should print 42 to the console
Get-SilentValue 999  # This should not output anything to the console