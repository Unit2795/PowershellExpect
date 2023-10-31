# PowershellExpect

Mimic Linux Expect, Spawn, and Send commands in Powershell for easy interactive CLI automation, without any external dependencies!

----

Installation
`Install-Module -Name PowershellExpect`

[More information about installing Powershell Gallery modules](https://learn.microsoft.com/en-us/powershell/module/powershellget/install-module?view=powershellget-2.x)

---

## Commands

### Spawn

#### Synopsis

Spawn a child PowerShell process to run commands in and optionally specify a command to run and a timeout for Expect statements (if a result is not received within the timeout, an error is thrown)

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type   | Example |
| ----------------------------- | ------------------------------------------------------------ | ------ | ------- |
| Command                       | Optional command to run in the spawned process               | string | node -v |
| Timeout                       | Optional global timeout in seconds to wait for Expect statements | int    | 10      |

#### Example

```powershell
Spawn "node -v" 10
```

### Send

#### Synopsis

Send text input to the spawned PowerShell process

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type    | Example    |
| ----------------------------- | ------------------------------------------------------------ | ------- | ---------- |
| Command*                      | Command to run in the spawned process                        | string  | node -v    |
| NoNewline                     | A newline is automatically appended, you can disable this behavior with this flag | boolean | -NoNewline |

#### Example

```powershell
Send "npm -v" -NoNewline
```

### Expect

#### Synopsis

Wait for a desired piece of text to appear in the spawned PowerShell process by searching for it with a regex

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type    | Example            |
| ----------------------------- | ------------------------------------------------------------ | ------- | ------------------ |
| Regex*                        | Regular expression to search for a match to                  | string  | node -v            |
| Timeout                       | Time in seconds to wait for a match to the regex             | int     | 10                 |
| ContinueOnTimeout             | Set this flag if you wish to continue despite a timeout, a message indicating the timeout will be displayed still | boolean | -ContinueOnTimeout |
| EOF                           | When you are finished and ready to kill the process, send the EOF (end of file) flag | boolean | -EOF               |

#### Example

```powershell
Expect "v18.*" -Timeout 10 -ContinueOnTimeout
Expect -EOF
```

### SendKey

#### Synopsis

Send a key input such as Enter, Up Arrow, Page Up, etc.

#### Parameters

| Parameter Name (* = Required) | Description                                   | Type          | Default | Example |
| ----------------------------- | --------------------------------------------- | ------------- | ------- | ------- |
| Key*                          | Key to send to the spawned PowerShell process | AvailableKeys |         | "up"    |

#### AvailableKeys

```
"up", "down", "left", "right", "enter", "esc", "space", "backspace", "tab", "delete", "home", "end", "pageup", "pagedown", "insert", "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12"
```

#### Example

```powershell
Expect "v18.*" -Timeout 10 -ContinueOnTimeout
Expect -EOF
```

### SendAndWaitForIdle

#### Synopsis

Send text input and wait for the output to be idle for a specified number of seconds

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type    | Default | Example    |
| ----------------------------- | ------------------------------------------------------------ | ------- | ------- | ---------- |
| Command*                      | Command to run in the spawned process                        | string  |         | node -v    |
| WaitForIdle                   | Time in seconds to wait for the output to be idle until returning the result | int     | 3       | 3          |
| IgnoreLines                   | Ignore a set number of lines from the beginning of the output, useful for ignoring the command input, which will be included in the result normally. (Ex: `PS C:\MyComputer\PowershellExpect> node -v`) | int     | 0       | 2          |
| NoNewline                     | A newline is automatically appended, you can disable this behavior with this flag | boolean | false   | -NoNewline |

#### Example

```powershell
Expect "v18.*" -Timeout 10 -ContinueOnTimeout
Expect -EOF
```



----

## Example Usage:
```powershell
# 2 second timeout, this example requires you to have something installed that can run bash scripts for you
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
