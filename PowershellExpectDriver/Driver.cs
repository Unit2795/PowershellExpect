using System.Diagnostics;

namespace PowershellExpectDriver
{
    public class PTYDriver
    {
        private PTY pty = new PTY();
        // Global timeout set by the spawn command
        private int? timeoutSeconds = null;
        // Whether logging has been enabled or not
        private bool loggingEnabled = false;
        // Buffer that contains the output of the process
        private List<string> output = new List<string>();
        
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
        
        private void HandleOutput(object? sender, string outputBuffer)
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
                
            // Set the max length of the output list to 100 items
            AppendOutput(outputBuffer, 100);
        }
    }
}