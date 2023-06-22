@{
    GUID = 'fc079b21-a082-4ad2-ab8f-387482b4c369'
    Author            = 'David Jozwik'
    Description       = 'Mimic Linux Expect, Spawn, and Send commands in Powershell without any external dependencies
    - Spawn -Command "npm run dev"
    - ExpectThenSend -Command "\r" -Expect ".*Press Enter.*"
    - Expect -Expect ".*Username.*"
    - Send "David\r"'
    FunctionsToExport = 'Expect', 'ExpectThenSend', 'Send', 'Spawn', 'Despawn', 'AsyncSpawn'
    ModuleVersion     = '1.0.1'
    RootModule        = 'PowershellExpect.psm1'
}