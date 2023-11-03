#nullable enable

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

/*
    C# driver that provides all of the functionality to PowershellExpect
 */
public class PowershellExpectHandler
{
    // Store a handler for the spawned PowerShell process
    Process process = new Process();
    // Buffer that contains the output of the process
    private List<string> output = new List<string>();
    // Global timeout set by the spawn command
    private int? timeoutSeconds = null;
    // Whether logging has been enabled or not
    private bool loggingEnabled = false;

    // Starts the PowerShell process, attaches listeners, and initializes variables
    public System.Diagnostics.Process StartProcess(string workingDirectory, int? timeout, bool enableLogging)
    {
        // Set the color of the output to help differentiate it from normal user terminal
        Console.ForegroundColor = ConsoleColor.Blue;
        
        // If a timeout was provided, override the global timeout
        if (timeout > 0)
        {
            timeoutSeconds = timeout;
        }
        // Assuming the user has enabled logging we'll set the global variable
        loggingEnabled = enableLogging;

        // Log info message about the process startup
        if (loggingEnabled)
        {
            InfoMessage("Starting process...");
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
        
        // Return the process
        return process;
    }

    // Log a message to keep the user appraised of progress
    private void InfoMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        // Revert back to default color after logging info message
        Console.ForegroundColor = ConsoleColor.Blue;
    }
    
    // Event handler for output messages. Places the output into an array of strings. 
    private void ProcessOutputHandler(object sender, DataReceivedEventArgs args)
    {
        void AppendOutput(string data, int maxLength)
        {
            // Log received output if logging is enabled
            if (loggingEnabled)
            {
                Console.WriteLine(data);
            }
        
            // If there are too many items in the array, truncate items starting from the oldest.
            if (output.Count > maxLength)
            {
                int removeCount = output.Count - maxLength;
                output.RemoveRange(0, removeCount);
            }

            output.Add(data);
        }
            
        if (args.Data != null)
        {
            // Set the max length of the output list to 100 items
            AppendOutput(args.Data, 100);
        }
    }
    
    // Close out the spawned process and detach event handlers
    public void Exit()
    {
        // Log info message about the process shutdown
        if (loggingEnabled)
        {
            InfoMessage("Closing process...");
        }
        
        Console.ResetColor();
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

        process.Close();
    }
    
    // Write a command to the spawned PowerShell process
    public void Send(string command, bool noNewline = false)
    {
        process.StandardInput.Write(command + (noNewline ? "" : "\n"));
    }
    
    // Write a command to the spawned PowerShell process and wait for the output buffer to be idle for a set duration
    public string SendAndWait(string command, int ignoreLines, int idleDurationSeconds, bool noNewline = false)
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
    
    // Observe the output of the spawned PowerShell process and wait for a desired result to be encountered
    public string? Expect(string regexString, int? timeoutMs, bool continueOnTimeout)
    {
        // Convert incoming regex string to actual Regex
        Regex regex = new Regex(regexString);
        // Variable for storing if a match has been received
        bool matched = false;
        // Variable for storing if there is a timeout, 0 indicates no timeout
        int? timeout = 0;
        
        // If a timeout was provided specifically to this expect, override any global settings
        if (timeoutMs > 0) 
        {
            timeout = timeoutMs;
        }
        //
        else if (timeoutSeconds > 0) 
        {
            timeout = timeoutSeconds;
        }
        // Calculate the max timestamp we can reach before the expect times out
        long? maxTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + timeout;
    
        // While no match is found (or no timeout occurs), continue to evaluate output until match is found
        do
        {
            // For each item found in the input, evaluate it for a match
            foreach (string item in output)
            {
                Match match = regex.Match(item);
                if (match.Success)
                {
                    // Log the match if logging is enabled
                    if (loggingEnabled)
                    {
                        InfoMessage("Match found: " + item);
                    }

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
                    this.Exit();
                    throw new Exception(timeoutMessage);
                }
                else
                {
                    InfoMessage(timeoutMessage);
                }
                break;
            }
            
            // TODO: Evaluate if this timeout is too much or if we should attempt to evaluate matches as they arrive.
            Thread.Sleep(500);
        } while (!matched);

        return null;
    }
}