using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;

public class ProcessStarter
{
    Process process = new Process();
    private static List<string> output = new List<string>();
    private int? timeoutSeconds = null;

    public void StartProcess(int? timeout)
    {
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
        process.CancelOutputRead();
        process.OutputDataReceived -= ProcessOutputHandler;
        Console.WriteLine("Killing process");
        if (!process.HasExited)
        {
            process.Kill();
        }
        process.Close();
    }
    
    public void Expect(string regexString, int? timeoutMs, bool continueOnTimeout, bool EOF)
    {
        if (EOF)
        {
            this.StopProcess();
        }
        else
        {
            Regex regex = new Regex(regexString);
            bool matched = false;
            int? timeout = 0;
            if (timeoutMs > 0) 
            {
                timeout = timeoutMs;
            }
            else if (timeoutSeconds > 0) 
            {
                timeout = timeoutSeconds;
            }
            long? maxTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + timeout;
        
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
                output.Clear();
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
        
        while (output.Count > maxLength)
        {
            output.RemoveAt(0);
        }

        output.Add(data);
    }
    
    private void ProcessOutputHandler(object sender, DataReceivedEventArgs args)
    {
        if (args.Data != null)
        {
            AppendOutput(args.Data, 100);
        }
    }
}

