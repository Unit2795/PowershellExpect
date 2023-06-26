# PowershellExpect

Mimic Linux Expect, Spawn, and Send commands in Powershell for easy CLI automation, without any external dependencies!

----

Installation
`Install-Module -Name PowershellExpect`

[More information about installing Powershell Gallery modules](https://learn.microsoft.com/en-us/powershell/module/powershellget/install-module?view=powershellget-2.x)

----

## Example Usage:
```powershell
# 2 second timeout
Spawn "bash ./test.sh" 2
Expect "name"
Send "Joe Tester"
Expect "age" 3 # Override global timeout (3 seconds for this expect statement)
Send "27"
Expect "salary"
Send "`$60,000"
Expect -EOF


Spawn "node -v"
Expect "v18.*"
Send "npm -v"
Expect "9.*"
Expect -EOF
```

---

Contributions welcome!

Missing a feature you need or require assistance? Open an issue or send a message in [Powershell Gallery](https://www.powershellgallery.com/packages/PowershellExpect)
