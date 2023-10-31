#nullable enable

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

public class PowershellExpectHandler
{
    Process process = new Process();
    private static List<string> output = new List<string>();
    private int? timeoutSeconds = null;

    public void StartProcess(string workingDirectory, int? timeout)
    {
        // If a timeout was provided, override the global timeout
        if (timeout > 0)
        {
            timeoutSeconds = timeout;
        }
        
        // Configure the process
        process.StartInfo.FileName = "pwsh.exe";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = false;
        process.EnableRaisingEvents = true;
        
        // Set the working directory to the current directory of the executing assembly
        process.StartInfo.WorkingDirectory = workingDirectory;

        // Attach an asynchronous event handler to the output
        process.OutputDataReceived += ProcessOutputHandler;
        process.ErrorDataReceived += ProcessOutputHandler;

        // Start the process
        process.Start();

        // Start reading the output asynchronously
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
    
    public void StopProcess()
    {
        // Stop reading the process output so we can remove the event handler
        process.CancelOutputRead();
        process.CancelErrorRead();
        process.OutputDataReceived -= ProcessOutputHandler;
        process.ErrorDataReceived -= ProcessOutputHandler;

        // Assuming process has not already exited, destroy the process
        if (!process.HasExited)
        {
            process.Kill();
        }
        Console.WriteLine("Closing process");
        process.Close();
    }
    
    public string? Expect(string regexString, int? timeoutMs, bool continueOnTimeout, bool EOF)
    {
        // If user is expecting end of automation process, close the process.
        if (EOF)
        {
            this.StopProcess();
        }
        else
        {
            // Convert incoming regex string to actual Regex
            Regex regex = new Regex(regexString);
            bool matched = false;
            int? timeout = 0;
            
            // If a timeout was provided specifically to this expect, override any global settings
            if (timeoutMs > 0) 
            {
                timeout = timeoutMs;
            }
            else if (timeoutSeconds > 0) 
            {
                timeout = timeoutSeconds;
            }
            // Calculate the max timestamp we can reach before the expect times out
            long? maxTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + timeout;
        
            // While no match is found (or no timeout occurs), continue to evaluate output until match is found
            do
            {
                foreach (string item in output)
                {
                    Match match = regex.Match(item);
                    if (match.Success)
                    {
                        Console.WriteLine("Match found: " + item);
                        matched = true;
                        return item;
                    }
                }
                // Clear the output to keep the buffer nice and lean
                output.Clear();
                
                // If a timeout is set and we've exceeded the max time, throw timeout error and stop the loop
                if (timeout > 0 && DateTimeOffset.Now.ToUnixTimeSeconds() >= maxTimestamp)
                {
                    string timeoutMessage = String.Format("Timed out waiting for: '{0}'", regexString);
                    matched = true;
                    if (!continueOnTimeout)
                    {
                        this.StopProcess();
                        throw new Exception(timeoutMessage);
                    }
                    else
                    {
                        Console.WriteLine(timeoutMessage);
                    }
                    break;
                }
                
                // TODO: Evaluate if this timeout is too much or if we should attempt to evaluate matches as they arrive.
                Thread.Sleep(500);
            } while (!matched);
        }

        return null;
    }

    public void Send(string command, bool noNewline = false)
    {
        process.StandardInput.Write(command + (noNewline ? "" : "\n"));
    }
    
    public string SendAndWaitForIdle(string command, int idleDurationSeconds, int ignoreLines, bool noNewline = false)
    {
        // Send the command
        Send(command, noNewline);

        // Capture the initial timestamp
        var startTime = DateTimeOffset.Now;

        // List to store captured output during idle time
        List<string> idleOutput = new List<string>();

        // Loop until the idle duration expires
        while (DateTimeOffset.Now < startTime.AddSeconds(idleDurationSeconds))
        {
            // We'll check for new output every 200ms. Adjust as necessary.
            Thread.Sleep(500);

            // Check if there's any new output
            if (output.Any())
            {
                // Add any new output to our idleOutput list
                idleOutput.AddRange(output);
                output.Clear(); // Clear the main output list to avoid double-capturing
            }
        }
        
        // Combine all captured lines into a single string
        var allOutput = string.Join("\n", idleOutput);

        // Split the string into lines, skip the first line, and then re-join them
        var trimmedOutput = string.Join("\n", allOutput.Split('\n').Skip(ignoreLines));

        // Return the captured output during idle time as a single string
        return trimmedOutput;
    }
    
    
    public void SendKeys(List<string> keyNames, bool simultaneous = false)
    {
        ConsoleModifiers modifiers = default;

        // First, process the modifier keys
        foreach (var keyName in keyNames)
        {
            if (keyName.ToLower() == "shift") modifiers |= ConsoleModifiers.Shift;
            else if (keyName.ToLower() == "alt") modifiers |= ConsoleModifiers.Alt;
            else if (keyName.ToLower() == "ctrl") modifiers |= ConsoleModifiers.Control;
        }

        // Then, process the actual keys
        foreach (var keyName in keyNames)
        {
            var key = GetConsoleKeyFromString(keyName);

            if (key == null)
            {
                throw new ArgumentException($"The key '{keyName}' is not supported. Please use a valid ConsoleKey name.");
            }

            // If the key is a modifier key, we skip since we've already processed them
            if (keyName.ToLower() == "shift" || keyName.ToLower() == "alt" || keyName.ToLower() == "ctrl") continue;

            var keyInfo = new ConsoleKeyInfo((char)key, key.Value, 
                                             (modifiers & ConsoleModifiers.Shift) != 0, 
                                             (modifiers & ConsoleModifiers.Alt) != 0, 
                                             (modifiers & ConsoleModifiers.Control) != 0);

            if (simultaneous)
            {
                // If simultaneous is set, combine the modifiers with each key and send
                SendKeyInfo(keyInfo);
            }
            else
            {
                // Otherwise, send each key individually
                SendKeyInfo(keyInfo);
            }
        }
    }

    private ConsoleKey? GetConsoleKeyFromString(string keyName)
    {
        foreach (ConsoleKey key in Enum.GetValues(typeof(ConsoleKey)))
        {
            if (key.ToString().Equals(keyName, StringComparison.OrdinalIgnoreCase))
                return key;
        }
        return null;  
    }

    private void SendKeyInfo(ConsoleKeyInfo keyInfo)
    {
        byte[] bytes;

        // If the key is alphanumeric, send only the char representation
        if (char.IsLetterOrDigit(keyInfo.KeyChar) || char.IsPunctuation(keyInfo.KeyChar) || char.IsWhiteSpace(keyInfo.KeyChar))
        {
            bytes = new byte[] { (byte)keyInfo.KeyChar };
        }
        else
        {
            bytes = new byte[] { (byte)keyInfo.Key };
        }
    
        process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
        process.StandardInput.BaseStream.Flush();
    }
    
    private static void AppendOutput(string data, int maxLength)
    {
        Console.WriteLine(data);
        
        // If there are too many items in the array, truncate items starting from the oldest.
        if (output.Count > maxLength)
        {
            int removeCount = output.Count - maxLength;
            output.RemoveRange(0, removeCount);
        }

        output.Add(data);
    }
    
    private void ProcessOutputHandler(object sender, DataReceivedEventArgs args)
    {
        if (args.Data != null)
        {
            // Set the max length of the output list to 100 items
            AppendOutput(args.Data, 100);
        }
    }
}

