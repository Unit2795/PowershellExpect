using System.Text.RegularExpressions;

namespace PowershellExpectDriver
{
    public class Driver
    {
        private readonly PTY pty = new();
        // Global timeout set by the spawn command
        private int timeoutSeconds;
        // Whether logging has been enabled or not
        private bool loggingEnabled;
        // Complete buffer (up to max size) used for matching
        private readonly CircularBuffer matchBuffer = new();
        // Buffer that is cleared after each send command, for returning results of individual commands
        private readonly CircularBuffer cmdBuffer = new();
        // Store the last output read timestamp for detecting idle duration.
        private long lastRead = 0;
        private bool hasObserver = false;
        
        public PTY Spawn(string workingDirectory, int timeout, bool enableLogging, string command = "pwsh")
        {
            if (timeout > 0)
                timeoutSeconds = timeout;
            
            loggingEnabled = enableLogging;
            
            InfoMessage("Starting process...");
            
            pty.HandleOutput += HandleOutput;
            pty.Spawn(command, workingDirectory);
            
            return pty;
        }
        
        public string? Send(string command, bool noNewLine, int idleDuration, int ignoreLines)
        {
            cmdBuffer.Clear();
            
            pty.Monitor?.GetSnapshot();
            
            pty.CopyInputToPipe(command, noNewLine);

            if (idleDuration <= 0) 
                return null;
            
            // Capture the initial timestamp
            var endTime = DateTimeOffset.Now.AddSeconds(idleDuration).ToUnixTimeMilliseconds();
            var previousRead = lastRead;

            // Loop until the idle duration expires
            while (DateTimeOffset.Now.ToUnixTimeMilliseconds() < endTime)
            {
                Thread.Sleep(500);
                
                // No new output has been read, continue to the next iteration
                if (lastRead == previousRead) 
                    continue;
                
                previousRead = lastRead;
                // If there is new output, reset the timer
                endTime = DateTimeOffset.Now.AddSeconds(idleDuration).ToUnixTimeMilliseconds();
            }

            if (ignoreLines > 0)
                // Split the string into lines, remove the requested number of lines from the start, and join them back together
                // Most useful for removing the command echo from the output
                cmdBuffer.Data = string.Join("\n", cmdBuffer.Data.Split('\n').Skip(ignoreLines));

            // Return the captured output during idle time as a single string
            return cmdBuffer.Data;
        }
        
        public struct ExpectData(string terminalOutput, string? match, int? exitCode)
        {
            public string TerminalOutput = terminalOutput;
            public string? Match = match;
            public int? ExitCode = exitCode;
        }
        
        public ExpectData? Expect(string regexString, int timeout, bool continueOnTimeout, bool eof)
        {
            pty.Monitor?.GetSnapshot();
            
            // Convert incoming regex string to actual Regex
            Regex regex = new Regex(regexString);
            
            // If no local timeout is set and a global timeout is set, use the global timeout
            if (timeout == 0 && timeoutSeconds > 0)
                timeout = timeoutSeconds;
            
            // Calculate the max timestamp we can reach before the expect times out
            long? maxTimestamp = DateTimeOffset.Now.AddSeconds(timeout).ToUnixTimeMilliseconds();
        
            // While no match is found (or no timeout occurs), continue to evaluate output until match is found
            while (true)
            {
                if (eof)
                {
                    var exitCode = pty.Monitor?.Metadata.ExitCode;
                    if (exitCode != null)
                    {
                        return new ExpectData
                        {
                            TerminalOutput = cmdBuffer.Data,
                            ExitCode = exitCode
                        };
                    }
                }
                else
                {
                    var match = regex.Match(matchBuffer.Data);
                    if (match.Success)
                    {
                        InfoMessage("Match found: " + match.Value);
                        return new ExpectData
                        {
                            TerminalOutput = cmdBuffer.Data,
                            Match = match.Value
                        };
                    }
                }
                
                // If a timeout is set and we've exceeded the max time, throw timeout error and stop the loop
                if (timeout > 0 && DateTimeOffset.Now.ToUnixTimeMilliseconds() >= maxTimestamp)
                {
                    var timeoutMessage = $"Timed out waiting for: '{( eof ? "EOF" : regexString )}'";
                    InfoMessage(timeoutMessage);
                    if (!continueOnTimeout)
                    {
                        Exit();
                        Environment.Exit(1);
                        // TODO: Fix unreachable exception and ensure stack trace works
                        throw new Exception(timeoutMessage);
                    }
                    break;
                }
                
                // TODO: Evaluate if this timeout is too much or if we should attempt to evaluate matches as they arrive.
                Thread.Sleep(500);
            }

            return null;
        }

        public void ShowTerminal(bool isInteractive)
        {
            if (!hasObserver)
            {
                pty.CreateObserver(isInteractive);
                hasObserver = true;
            } else {
                pty.FocusObserver(isInteractive);
            }
        }

        public void HideTerminal()
        {
            if (hasObserver)
            {
                pty.DestroyObserver();
            }
            
            hasObserver = false;
        }

        public ProcessMetadata? SpawnInfo() => pty.Monitor?.Metadata;
        
        public void Exit()
        {
            pty.Monitor?.GetSnapshot();
            
            // Log info message about the process shutdown
            InfoMessage("Closing process...");
            
            pty.HandleOutput -= HandleOutput;
            pty.DisposeResources();
        }
        
        // Add PTY output to the matching buffer
        private void HandleOutput(object? sender, string outputBuffer)
        {
            if (outputBuffer.Length <= 0) return;
            matchBuffer.Data = outputBuffer;
            lastRead = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            cmdBuffer.Data = outputBuffer;
        }

        // Log a message to keep the user appraised of progress
        private void InfoMessage(string message)
        {
            if (loggingEnabled)
                Console.WriteLine("[PowershellExpect] " + message);
        }
    }
}