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

        public string ReadLines(int bottomLines = 9000)
        {
            // Read last N lines from the file
            var lines = File.ReadLines(this.Path).TakeLast(bottomLines);
            return string.Join(Environment.NewLine, lines);
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
        private readonly string tempFilePath = Path.GetTempFileName();

        public void Log(string message)
        {
            using var writer = File.AppendText(tempFilePath);
            writer.Write(message);
        }

        public void Cleanup() => File.Delete(tempFilePath);
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