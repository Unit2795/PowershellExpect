# PowershellExpect

Mimic Linux Expect, Spawn, and Send commands in Powershell for easy interactive CLI automation, without any external dependencies!

---

## Motivation

This library helps automate some of those CLIs that are designed with interactivity in mind and an easier to automate programmatic CLI/API is not available.

Not every CLI, GUI and other flows will be able to be automated with this tool and you may need to investigate more advanced RPA solutions. Like most automations like this, it's also possible that an update will break the functionality of your script or this library. It's also difficult to send simulated keystrokes (arrow keys, ctrl, etc) to CLIs with this library, if you find you need this feature, open an issue and we may consider looking into this more seriously.

### You may not need this library

A library like this may be overkill when all you need to do is take the result of a command and pipe it into a variable. PowerShell on its own is already quite capable of producing advanced automations. Evaluate your usecase carefully and try to automate without a library first. If you need to work with long running processes, share results between multiple simultaneously running processes, or simplify your automation script; this library may be helpful. If you find that this library is missing a feature, please create a GitHub issue!

```powershell
# Example of forwarding the result of a command into a variable
$nodeVersion = & node -v
if ($nodeVersion -match "V18.*")
{
	Write-Host "Node.js version satisfactory!"
}
Write-Host "Node.js Version: $nodeVersion"
```

---

## Features

- Run multiple processes in parallel and share data between them
- Expect results in the output using regex or wait until the output is idle for a specified duration
- Store outputs retrieved from Send commands or Expect statements for processing

---

## Installation

`Install-Module -Name PowershellExpect`

[More information about installing Powershell Gallery modules](https://learn.microsoft.com/en-us/powershell/module/powershellget/install-module?view=powershellget-2.x)

---

## Example

**Simple Example**

Simple script that checks the version of Node.JS programming language and NPM package manager

```powershell
# Import the PowershellExpect module
Import-Module PowershellExpect

$session = Spawn -Timeout 5 -EnableLogging
$session.Send("node -v")
$node = $session.Expect("v18.*")
$session.Send("npm -v", $true)
$npm = $session.Expect("10.*")
$session.Exit()

Write-Host "Node Version: $node"
Write-Host "NPM Version: $npm"
```

**Advanced Example**

Log into a server, retrieve a list of files, extract data from a file, check system status, log out, and log results

```powershell
# Import the PowershellExpect module
Import-Module PowershellExpect

# Spawn a new powershell process to send commands to, set a 5 second expect timeout, and enable logging
$session = Spawn -Timeout 5 -EnableLogging

# Send the username and password
$session.Send("admin")
$session.Send("secretPass")

# Check for successful login
if ($session.Expect("Welcome to the server")) {
    # List files
    $session.Send("list_files")
    $fileList = $session.Expect("Files: (.*)") # Using regex to capture file list

    # Check for "important.txt" in the output
    if ($fileList -like "*important.txt*") {
        $session.Send("extract_data important.txt")
        $output = $session.Expect("Data: (.*)") # Extracting data from important.txt
    }

    # Check system status
    $session.Send("system_status")
    $status = $session.Expect("Status: (GOOD|FAIR|BAD|CRITICAL)")

    if ($status -match "GOOD|FAIR") {
        Write-Host "System status is acceptable."
    } elseif ($status -match "BAD|CRITICAL") {
        Write-Host "System status is problematic."
        exit 1
    }

    # Logout
    $session.Send("logout")
    if ($session.Expect("Goodbye")) {
        Write-Host "Logged out successfully."
    }

} elseif ($session.Expect("Login Failed")) {
    Write-Host "Login failed!"
    exit 1
}

# Display extracted data if available
if ($output) {
    Write-Host "Extracted data from important.txt: $output"
}

# Close out the session
$session.Exit()
```

---

## Commands

### Spawn

#### Synopsis

Spawn a child PowerShell process to run commands in and optionally specify a timeout for Expect statements (if a result is not received within the timeout, an error or warning is thrown). Returns a process handler object that you will issue all subsequent commands to.

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type   | Default | Example        |
| ----------------------------- | ------------------------------------------------------------ | ------ | ------- | -------------- |
| Timeout                       | Optional global timeout in seconds to wait for Expect statements | int    | None    | 10             |
| EnableLogging                 | Specify if the process should include logging (logs output and info/events) | switch | false   | -EnableLogging |

#### Example

```powershell
$myProcess = Spawn 10 -EnableLogging
```

### Send

#### Synopsis

Send text input to the spawned PowerShell process

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type    | Default | Example    |
| ----------------------------- | ------------------------------------------------------------ | ------- | ------- | ---------- |
| Command*                      | Command to run in the spawned process                        | string  |         | node -v    |
| NoNewline                     | A newline is automatically appended, you can disable this behavior with this flag and then send the newline character manually as you wish "\n" | boolean | false   | -NoNewline |

#### Example

```powershell
$myProcess.Send("node -v", $false)
```

### Expect

#### Synopsis

Wait for a desired piece of text to appear in the spawned PowerShell process by searching for it with a regex. The found result is returned and may be stored in a variable.

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type    | Default | Example            |
| ----------------------------- | ------------------------------------------------------------ | ------- | ------- | ------------------ |
| Regex*                        | Regular expression to search for a match to                  | string  |         | node -v            |
| Timeout                       | Time in seconds to wait for a match to the regex             | int     | None    | 10                 |
| ContinueOnTimeout             | Set this flag if you wish to continue despite a timeout, a message indicating the timeout will be displayed still | boolean | false   | -ContinueOnTimeout |

#### Example

```powershell
$nodeVersion = $myProcess.Expect("v18.*", 10, $true)
$myProcess.Expect("login success")
```

### SendAndWait

#### Synopsis

Send text input and wait for the output to be idle for a specified number of seconds, then return the captured output

#### Parameters

| Parameter Name (* = Required) | Description                                                  | Type    | Default | Example    |
| ----------------------------- | ------------------------------------------------------------ | ------- | ------- | ---------- |
| Command*                      | Command to run in the spawned process                        | string  |         | node -v    |
| IgnoreLines                   | Ignore a set number of lines from the beginning of the output, useful for ignoring the command input, which will be included in the result normally. (Ex: `PS C:\MyComputer\PowershellExpect> node -v`) | int     | 0       | 2          |
| WaitForIdle                   | Time in seconds to wait for the output to be idle until returning the result | int     | 3       | 3          |
| NoNewline                     | A newline is automatically appended, you can disable this behavior with this flag | boolean | false   | -NoNewline |

#### Example

```powershell
$pnpm = $myProcess.SendAndWait("pnpm -v", 2, 10, $false)
$myFiles = $myProcess.SendAndWait("Files: .*")
```

### Exit

#### Synopsis

Destroy the spawned PowerShell process. **Remember to do this, or your process may continue running in the background until you close out your PowerShell terminal**

#### Example

```powershell
$myProcess.Exit()
```

---

## Support

Need help setting up your automation, want to request a feature, or encountered a bug? Please submit an issue to the PowerShell Expect repository or send a message in [Powershell Gallery](https://www.powershellgallery.com/packages/PowershellExpect)!

---

## Contributions

Contributions welcome! Submit an issue to the repo expressing interest or create a PR/branch! This library is MIT licensed. You are free to utilize it as you please.
