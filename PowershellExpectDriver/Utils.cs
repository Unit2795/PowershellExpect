using System.Text;

namespace PowershellExpectDriver
{
    public class Logger
    {
        private readonly string tempFilePath;

        public Logger()
        {
            tempFilePath = Path.GetTempFileName();
        }

        public void Log(string message)
        {
            // TODO: Max write every second
            using (StreamWriter writer = File.AppendText(tempFilePath))
            {
                writer.Write(message);
            }
        }

        public void Cleanup()
        {
            File.Delete(tempFilePath);
        }
    }
    
    public class CircularBuffer
    {
        private StringBuilder buffer = new();
        private readonly int size;

        public CircularBuffer(int maxSize = 8192)
        {
            if (maxSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be greater than zero.");
            
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
            {
                buffer.Remove(0, buffer.Length - size);
            }
        }
        
        public void Clear() => buffer.Clear();

        public override string ToString() => buffer.ToString();
    }
}