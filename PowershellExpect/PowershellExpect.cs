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

    public void StartProcess(int? timeout)
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
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = false;
        process.EnableRaisingEvents = true;

        // Attach an asynchronous event handler to the output
        process.OutputDataReceived += ProcessOutputHandler;

        // Start the process
        process.Start();

        // Start reading the output asynchronously
        process.BeginOutputReadLine();
    }
    
    public void StopProcess()
    {
        // Stop reading the process output so we can remove the event handler
        process.CancelOutputRead();
        process.OutputDataReceived -= ProcessOutputHandler;

        // Assuming process has not already exited, destroy the process
        if (!process.HasExited)
        {
            process.Kill();
        }
        Console.WriteLine("Closing process");
        process.Close();
    }
    
    public void Expect(string regexString, int? timeoutMs, bool continueOnTimeout, bool EOF)
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
                        break;
                    }
                }
                // Clear the output to keep the buffer nice and lean
                output.Clear();
                
                // If a timeout is set and we've exceeded the max time, throw timeout error and stop the loop
                if (timeout > 0 && DateTimeOffset.Now.ToUnixTimeSeconds() >= maxTimestamp)
                {
                    string timeoutMessage = $"Timed out waiting for: '{regexString}'";
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
    }

    public void Send(string command, bool noNewline)
    {
        process.StandardInput.Write(command + (noNewline ? "" : "\n"));
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

