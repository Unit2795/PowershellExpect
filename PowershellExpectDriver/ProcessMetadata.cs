using System.Diagnostics;

namespace PowershellExpectDriver
{
    public record ProcessMetadata
    {
        public int ProcessId { get; init; }
        public string? ProcessName { get; init; }
        public long StartTime { get; init; }
        public long? ExitTime { get; private set; }
        public long Elapsed => ExitTime is not null ? ExitTime.Value - StartTime : DateTimeOffset.Now.ToUnixTimeMilliseconds() - StartTime;
        public long MemoryUsage { get; private set; }
        public long PeakMemoryUsage { get; private set; }
        public int HandleCount { get; private set; }
        public int ThreadCount { get; private set; }
        public int PeakHandleCount { get; private set; }
        public int PeakThreadCount { get; private set; }
        public int BasePriority { get; private set; }
        public ProcessPriorityClass PriorityClass { get; private set; }
        public double TotalProcessorTime { get; private set; }
        public double UserProcessorTime { get; private set; }
        public double PrivilegedProcessorTime { get; private set; }
        public int? ExitCode { get; private set; }
        
        internal void UpdateOnExit(int exitCode, long exitTime)
        {
            ExitCode = exitCode;
            ExitTime = exitTime;
        }

        internal void UpdateSnapshot(System.Diagnostics.Process process)
        {
            MemoryUsage = process.WorkingSet64;
            PeakMemoryUsage = process.PeakWorkingSet64;
            HandleCount = process.HandleCount;
            ThreadCount = process.Threads.Count;
            PeakHandleCount = Math.Max(PeakHandleCount, process.HandleCount);
            PeakThreadCount = Math.Max(PeakThreadCount, process.Threads.Count);
            BasePriority = process.BasePriority;
            PriorityClass = process.PriorityClass;
            TotalProcessorTime = process.TotalProcessorTime.TotalMilliseconds;
            UserProcessorTime = process.UserProcessorTime.TotalMilliseconds;
            PrivilegedProcessorTime = process.PrivilegedProcessorTime.TotalMilliseconds;
        }
    }

    public class ProcessMonitor : IDisposable
    {
        public ProcessMetadata Metadata { get; private set; }
        
        private readonly System.Diagnostics.Process process;
        private bool disposed = false;

        public ProcessMonitor(int processId)
        {
            process = System.Diagnostics.Process.GetProcessById(processId);
            process.EnableRaisingEvents = true;
            process.Exited += OnProcessExited;

            Metadata = new ProcessMetadata
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                StartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };
        }

        public void GetSnapshot()
        {
            if (Metadata.ExitCode.HasValue || disposed) return;
            
            Metadata.UpdateSnapshot(process);
        }
        
        private void OnProcessExited(object? sender, EventArgs e)
        {
            Metadata.UpdateOnExit(process.ExitCode, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            process.Exited -= OnProcessExited;
            process.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}