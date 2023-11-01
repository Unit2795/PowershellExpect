@{
    GUID = 'fc079b21-a082-4ad2-ab8f-387482b4c369'
    Author            = 'David Jozwik'
    Description       = 'Mimic Linux Expect, Spawn, and Send commands in Powershell for easy CLI automation, without any external dependencies!
    
    More info & examples: https://github.com/Unit2795/PowershellExpect
    
    Keywords:
    automation, RPA, expect, spawn, send, linux, bash, CLI, async, thread-safe, regex
    
    MIT licensed!
    '
    FunctionsToExport = 'Expect', 'Send', 'Spawn'
    ModuleVersion     = '1.2.0'
    RootModule        = 'PowershellExpect.psm1'
    PowerShellVersion = '7.0'
    PrivateData = @{
        PSData = @{
            Tags = 'automation', 'RPA', 'expect', 'spawn', 'send', 'linux', 'bash', 'CLI', 'regex'
            ProjectUri = 'https://www.powershellgallery.com/packages/PowershellExpect'
        }
    }
}