@{
    GUID = 'fc079b21-a082-4ad2-ab8f-387482b4c369'
    Author            = 'David Jozwik'
    Description       = 'Mimic Linux Expect, Spawn, and Send commands in Powershell for easy CLI automation, without any external dependencies! MIT license
    Examples and more info: https://github.com/Unit2795/PowershellExpect

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
    
    
    Keywords:
    automation, RPA, expect, spawn, send, linux, bash, CLI, async, thread-safe, regex
    '
    FunctionsToExport = 'Expect', 'Send', 'Spawn'
    ModuleVersion     = '1.1.0'
    RootModule        = 'PowershellExpect.psm1'
    PowerShellVersion = '5.0'
    PrivateData = @{
        PSData = @{
            Tags = 'automation', 'RPA', 'expect', 'spawn', 'send', 'linux', 'bash', 'CLI', 'regex'
            ProjectUri = 'https://www.powershellgallery.com/packages/PowershellExpect'
        }
    }
}