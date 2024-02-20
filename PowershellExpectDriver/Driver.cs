using System.Text.RegularExpressions;

namespace PowershellExpectDriver
{
    public class Driver
    {
        private PTY pty = new PTY();
        // Global timeout set by the spawn command
        private int? timeoutSeconds = null;
        // Whether logging has been enabled or not
        private bool loggingEnabled = false;
        // Buffer that contains the output of the process
        private string output = "";
        
        public PTY StartProcess(string workingDirectory, int? timeout, bool enableLogging)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            if (timeout > 0)
            {
                timeoutSeconds = timeout;
            }
            loggingEnabled = enableLogging;
            
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
        
        public void Send(string command, bool noNewline = false)
        {
            pty.CopyInputToPipe(command, noNewline);
        }
        
        public struct ExpectData(string terminalOutput, string match)
        {
            public string terminalOutput = terminalOutput;
            public string match = match;
        }
        
        public ExpectData? Expect(string regexString, int? timeoutSec, bool continueOnTimeout)
        {
            // Convert incoming regex string to actual Regex
            Regex regex = new Regex(regexString);
            // Variable for storing if a match has been received
            bool matched = false;
            // Variable for storing if there is a timeout, 0 indicates no timeout
            int? timeout = 0;
            
            // If a timeout was provided specifically to this expect, override any global settings
            if (timeoutSec > 0)
            {
                timeout = timeoutSec;
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
                Match match = regex.Match(output);
                if (match.Success)
                {
                    // Log the match if logging is enabled
                    /*if (loggingEnabled)
                    {
                        InfoMessage("Match found: " + item);
                    }*/

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
                    if (!continueOnTimeout)
                    {
                        /*this.Exit();*/
                        throw new Exception(timeoutMessage);
                    }
                    /*else
                    {
                        InfoMessage(timeoutMessage);
                    }*/
                    break;
                }
                
                // TODO: Evaluate if this timeout is too much or if we should attempt to evaluate matches as they arrive.
                Thread.Sleep(500);
            } while (!matched);

            return null;
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
    }
}