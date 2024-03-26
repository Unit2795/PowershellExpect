using System.Text;

namespace PowershellExpectDriver
{
    public class TerminalBuffer
    {
        private const ushort ChunkSize = 1024 * 4; // 4KB
        private const uint BufferSize = 1024 * 128; // 128KB
        private byte[] byteBuffer = new byte[BufferSize];
        private int writePosition = 0;
        public string Path { get; } = System.IO.Path.GetTempFileName();

        public void Append(ReadOnlyMemory<byte> incomingData)
        {
            Buffer.BlockCopy(incomingData.ToArray(), 0, byteBuffer, writePosition, incomingData.Length);
            writePosition += incomingData.Length;
            
            // If less than 4KB of buffer is available, flush the buffer to file 
            if ((BufferSize - writePosition) < ChunkSize)
                Flush();
        }
        
        public void Append(string incomingData)
        {
            // Convert incoming string to bytes
            var bytes = System.Text.Encoding.UTF8.GetBytes(incomingData);
    
            // Call the primary Append method with byte array
            // Note: new ReadOnlyMemory<byte>(bytes) wraps byte array in a ReadOnlyMemory structure
            Append(new ReadOnlyMemory<byte>(bytes));
        }

        // Memory efficient method to read the last n lines of a file
        public string ReadLastLines(int lineCount = 9001)
        {
            var lineEndingsEncountered = 0;
            var buffer = new byte[1];
            StringBuilder stringBuilder = new StringBuilder();

            using (var fileStream = new FileStream(this.Path, FileMode.Open, FileAccess.Read))
            {
                // Start from the end of the file.
                var position = fileStream.Length;

                while (position > 0)
                {
                    fileStream.Seek(--position, SeekOrigin.Begin);
                    fileStream.Read(buffer, 0, 1);

                    // '\n' is the newline character
                    if (buffer[0] != '\n') continue;
                    
                    lineEndingsEncountered++;

                    if (lineEndingsEncountered == lineCount)
                    {
                        // Found the start of the (lineCount)th line.
                        break;
                    }
                }

                // Read from the current position to the end of the file.
                var remainingBytes = new byte[fileStream.Length - position];
                fileStream.Read(remainingBytes, 0, remainingBytes.Length);
                stringBuilder.Append(Encoding.UTF8.GetString(remainingBytes));
            }

            return stringBuilder.ToString();
        }
        
        public void Flush()
        {
            using var fileStream = new FileStream(Path, FileMode.Open, FileAccess.Write);
            fileStream.Write(byteBuffer, 0, writePosition);
            fileStream.Flush();
            writePosition = 0;
        }
        
        public void Clear()
        {
            writePosition = 0;
            File.Delete(Path);
        }
    }
    
    public class Logger
    {
        public readonly string FilePath = Path.GetTempFileName();

        public void Log(string message)
        {
            using var writer = File.AppendText(FilePath);
            writer.Write(message);
        }

        public void Cleanup() => File.Delete(FilePath);
    }
    
    public class CircularBuffer
    {
        private StringBuilder buffer = new();
        private readonly int size;

        public CircularBuffer(int maxSize = 8192)
        {
            if (maxSize <= 0) 
                throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be greater than zero.");
            
            size = maxSize;
        }

        public string Data
        {
            set => Append(value);
            get => buffer.ToString();
        }

        public void Append(string data)
        {
            buffer.Append(data);
            if (buffer.Length > size)
                buffer.Remove(0, buffer.Length - size);
        }
        
        public void Clear() => buffer.Clear();

        public override string ToString() => buffer.ToString();
    }
}