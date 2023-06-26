# 2 second timeout
Spawn "bash ./test.sh" 2
Expect "name"
Send "Joe Tester"
Expect "age"
Send "27"
Expect "salary"
Send "`$60,000"
Expect -EOF


Spawn "node -v"
Expect "v18.*"
Send "npm -v"
Expect "9.*"
Expect -EOF