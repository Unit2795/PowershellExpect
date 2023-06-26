Import-Module "..\src\PowershellExpect.psm1"

# 2 second timeout
Spawn "bash ./examples/test.sh" 3
Expect "name"
Send "Joe Tester"
Expect "age"
Send "27"
Expect "salary"
Send "`$60,000"
Expect -EOF


Spawn "node -v" 6
Expect "v18.*"
Send "npm -v"
Expect "9.*"
Expect -EOF