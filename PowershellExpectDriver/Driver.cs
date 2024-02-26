using System.Text.RegularExpressions;

namespace PowershellExpectDriver
{
    public class Driver
    {
        private PTY pty = new PTY();
        // Global timeout set by the spawn command
        private int timeoutSeconds = 0;
        // Whether logging has been enabled or not
        private bool loggingEnabled = false;
        // Buffer that contains the output of the process
        private string output = "";
        
        public PTY Spawn(string workingDirectory, int timeout, bool enableLogging, bool showTerminal)
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
                
                pty.Run(workingDirectory);
                
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
            var idleOutput = output; 

            // Loop until the idle duration expires
            while (DateTimeOffset.Now < endTime)
            {
                Thread.Sleep(200);

                // If there is new output, append it to the idle output and reset the timer
                if (output.Length > 0)
                {
                    startTime = DateTimeOffset.Now;
                    endTime = startTime.AddSeconds(idleDuration);
                    
                    // Append any new output and clear the buffer
                    idleOutput += output;
                    output = "";
                }
            }

            if (ignoreLines > 0)
            {
                // Split the string into lines, remove the requested number of lines from the start, and join them back together
                // Most useful for removing the command echo from the output
                idleOutput = string.Join("\n", idleOutput.Split('\n').Skip(ignoreLines));
            }

            // Return the captured output during idle time as a single string
            return idleOutput;
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
            // Variable for storing if a match has been received
            bool matched = false;
            
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
                Match match = regex.Match(output);
                if (match.Success)
                {
                    // Log the match if logging is enabled
                    if (loggingEnabled)
                    {
                        InfoMessage("Match found: " + match.Value);
                    }

                    matched = true;
                    return new ExpectData(output, match.Value);
                }
                // Clear the output to keep the buffer nice and lean
                output = "";
                
                // If a timeout is set and we've exceeded the max time, throw timeout error and stop the loop
                if (timeout > 0 && DateTimeOffset.Now.ToUnixTimeSeconds() >= maxTimestamp)
                {
                    string timeoutMessage = String.Format("Timed out waiting for: '{0}'", regexString);
                    matched = true;
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
            } while (!matched);

            return null;
        }
        
        // TODO
        /*public void ShowTerminal()
        {
            pty.ShowTerminal();
        }*/
        
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
            const int maxLength = 8192;
            string newOutput = output + outputBuffer;
            if (newOutput.Length > maxLength)
            {
                // Calculate the number of characters to remove from the start of the combined string.
                int charsToRemove = newOutput.Length - maxLength;

                // Remove the oldest characters from the start of the string to meet the maximum length constraint.
                newOutput = newOutput.Substring(charsToRemove);
            }
            
            output = newOutput;
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