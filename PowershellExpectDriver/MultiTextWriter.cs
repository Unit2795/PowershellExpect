using System.Text;

namespace PowershellExpectDriver
{
    public class MultiTextWriter : TextWriter
    {
        private TextWriter originalOut;
        private TerminalBuffer terminalBuffer = new();
        private bool writeToConsole;

        public MultiTextWriter(bool initiallyWriteToConsole = true)
        {
            originalOut = Console.Out;
            writeToConsole = initiallyWriteToConsole;
        }

        public void SetWriteToConsole(bool shouldWrite)
        {
            writeToConsole = shouldWrite;
            
            if (!shouldWrite) return;
            
            terminalBuffer.Flush();
            originalOut.Write(terminalBuffer.ReadLines());
        }

        public override Encoding Encoding => originalOut.Encoding;

        public override void Write(string? value)
        {
            if (value == null) return;
            terminalBuffer.Append(value);
            if (writeToConsole)
            {
                originalOut.Write(value);
            }
        }
    }
}