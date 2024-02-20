# Description: This file contains helper functions that can be used in other scripts

<# 
    Check if the line contains variable assignment syntax before calling a function

    This allows us to prevent a function from printing a value to the terminal if it is not being assigned to a variable
#>
function Test-OutputCaptured {
    <#
        Get the call context of the function that called this helper function
        
        Note that this only traverses up one level of the call stack, if it is higher, this will 
        not work and we may need to consider allowing the user to pass in a layer number as a param.
    #>
    $callerInvocationLine = (Get-PSCallStack)[1].InvocationInfo.Line

    # Check if the line contains variable assignment syntax
    if ($callerInvocationLine -match "^\s*\$\w+\s*=") {
        return $true
    } else {
        return $false
    }
}