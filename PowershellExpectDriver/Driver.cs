using System.IO.Pipes;
using System.Text.RegularExpressions;

namespace PowershellExpectDriver
{
    public class Driver
    {
        private readonly PTY pty = new PTY();
        // Global timeout set by the spawn command
        private int timeoutSeconds;
        // Whether logging has been enabled or not
        private bool loggingEnabled;
        // Buffer that contains the output of the process
        private readonly CircularBuffer buffer = new();
        
        public PTY Spawn(string workingDirectory, int timeout, bool enableLogging, bool showTerminal, string dllPath)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            if (timeout > 0)
            {
                timeoutSeconds = timeout;
            }
            loggingEnabled = enableLogging;
            
            if (loggingEnabled)
            {
                InfoMessage("Starting process...");
            }
            
            try
            {
                pty.OutputReceived += HandleOutput;
                
                pty.CreateObserver(dllPath);
                
                pty.Run();
                
                if (showTerminal)
                {
                    ShowTerminal();
                }
                
                return pty;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        public string? Send(string command, bool noNewLine, int idleDuration, int ignoreLines)
        {
            pty.CopyInputToPipe(command, noNewLine);

            if (idleDuration <= 0) return null;
            
            // Capture the initial timestamp
            var startTime = DateTimeOffset.Now;
            var endTime = startTime.AddSeconds(idleDuration);
            var idleOutput = new CircularBuffer
            {
                Data = buffer.Data
            };

            // Loop until the idle duration expires
            while (DateTimeOffset.Now < endTime)
            {
                Thread.Sleep(200);

                // If there is new output, append it to the idle output and reset the timer
                if (buffer.Data.Length <= 0) continue;
                startTime = DateTimeOffset.Now;
                endTime = startTime.AddSeconds(idleDuration);
                    
                // Append any new output and clear the buffer
                idleOutput.Data = buffer.Data;
                buffer.Clear();
            }

            if (ignoreLines > 0)
            {
                // Split the string into lines, remove the requested number of lines from the start, and join them back together
                // Most useful for removing the command echo from the output
                idleOutput.Data = string.Join("\n", idleOutput.Data.Split('\n').Skip(ignoreLines));
            }

            // Return the captured output during idle time as a single string
            return idleOutput.Data;
        }
        
        public struct ExpectData(string terminalOutput, string match)
        {
            public string terminalOutput = terminalOutput;
            public string match = match;
        }
        
        public ExpectData? Expect(string regexString, int timeout, bool continueOnTimeout, bool eof)
        {
            if (eof)
            {
                Exit();
                return null;
            }
            
            // Convert incoming regex string to actual Regex
            Regex regex = new Regex(regexString);
            
            // If no timeout is set and a global timeout is set, use the global timeout
            if (timeout == 0 && timeoutSeconds > 0)
            {
                timeout = timeoutSeconds;
            }
            // Calculate the max timestamp we can reach before the expect times out
            long? maxTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds() + timeout;
        
            // While no match is found (or no timeout occurs), continue to evaluate output until match is found
            do
            {
                var match = regex.Match(buffer.Data);
                if (match.Success)
                {
                    // Log the match if logging is enabled
                    if (loggingEnabled)
                    {
                        InfoMessage("Match found: " + match.Value);
                    }
                    
                    return new ExpectData(buffer.Data, match.Value);
                }
                buffer.Clear();
                
                // If a timeout is set and we've exceeded the max time, throw timeout error and stop the loop
                if (timeout > 0 && DateTimeOffset.Now.ToUnixTimeSeconds() >= maxTimestamp)
                {
                    var timeoutMessage = $"Timed out waiting for: '{regexString}'";
                    InfoMessage(timeoutMessage);
                    if (continueOnTimeout != true)
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
            } while (true);

            return null;
        }
        
        public void ShowTerminal()
        {
            pty.ShowObserver();
        }
        
        public void HideTerminal()
        {
            pty.HideObserver();
        }

        public static void AttachPipe()
        {
            var server = new NamedPipeServerStream("observer", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            server.WaitForConnection();
            var sr = new StreamReader(server);
            while (true)
            {
                if (server.IsConnected)
                {
                    var message = sr.ReadLine();
                    if (message != null)
                    {
                        Console.WriteLine(message);
                    }
                }
                else
                {
                    Thread.Sleep(1000); // Wait a bit before trying again
                }
            }
        }
        
        private void Exit()
        {
            // Log info message about the process shutdown
            if (loggingEnabled)
            {
                InfoMessage("Closing process...");
            }
            
            pty.OutputReceived -= HandleOutput;
            
            pty.DisposeResources();
        }
        
        private void HandleOutput(object? sender, string outputBuffer)
        {
            buffer.Data = outputBuffer;
        }
        
        // Log a message to keep the user appraised of progress
        private void InfoMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[PowershellExpect] " + message);
            // Revert back to default color after logging info message
            Console.ForegroundColor = ConsoleColor.Blue;
        }
    }
}