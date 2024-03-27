using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PTY
    {
        // Event handler for output received from the PTY/child process
        public event EventHandler<string>? HandleOutput;
        public ProcessMonitor? Monitor;

        private bool disposed = false;
        // Pipes for input and output to the PTY
        private PTYPipe? inputPipe;
        private PTYPipe? outputPipe;
        // PTY process and child process
        private PTYHandler? ptyProcess;
        private Process? pwshProcess;
        // Buffer for storing PTY output
        private TerminalBuffer terminalBuffer = new();
        // Mutex for halting output reading while observer terminal is booting
        private bool readPaused = false;
        private string sessionId = GenerateSessionId();
        private string? dllDirectory;
        private string dllPath = Assembly.GetExecutingAssembly().Location;
        
        private System.Diagnostics.Process? observerProcess;
        private IntPtr observerWindowHandle;
        private bool observerInteractive;
        private System.IO.Pipes.NamedPipeServerStream? observerOutput;
        private System.IO.Pipes.NamedPipeServerStream? observerInput;
        private System.IO.Pipes.NamedPipeServerStream? observerResize;
        private string observerOutputPipeName;
        private string observerInputPipeName;
        private string observerResizePipeName;
        private string observerMutexName;
        private Mutex? observerMutex;
        private int initialPtyWidth;
        private int initialPtyHeight;
        private int ptyWidth;
        private int ptyHeight;
        private System.Timers.Timer resizeTimer;
        private bool resizeTriggered;
        private Logger logger = new();
        
        public PTY()
        {
            observerOutputPipeName = $"{sessionId}ObserverOutput";
            observerInputPipeName = $"{sessionId}ObserverInput";
            observerResizePipeName = $"{sessionId}ObserverResize";
            observerMutexName = $"{sessionId}ObserverMutex";

            // TODO: Remove logger before release
            Console.WriteLine(logger.FilePath);

            dllDirectory = Path.GetDirectoryName(dllPath);
            // TODO: Use this to load observer script or remove it
            // childScriptPath = Path.Join(dllDirectory, "ChildScript.ps1");
        }
        
        // Spawn a new PTY and child process
        public void Spawn(string command, string workingDirectory, int width, int height)
        {
            inputPipe = new PTYPipe();
            outputPipe = new PTYPipe();
            ptyProcess = PTYHandler.Create(inputPipe.ReadSide, outputPipe.WriteSide, width, height);
            pwshProcess = ProcessFactory.Start(PTYHandler.PseudoConsoleThreadAttribute, ptyProcess.Handle, command, workingDirectory);
            Monitor = new ProcessMonitor(pwshProcess.ProcessInfo.dwProcessId);
            initialPtyWidth = width;
            initialPtyHeight = height;
            ptyWidth = width;
            ptyHeight = height;
            
            _ = CopyPipeToOutput(outputPipe.ReadSide);
            
            // Free resources if case the console is ungracefully closed (e.g. by the 'x' in the window titlebar or CTRL+C)
            OnClose(DisposeResources);

            // Observe process and dispose resources when it exits
            var waitHandle = new TaskCompletionSource<bool>();
            new Thread(() =>
            {
                // Block the thread until the process exits
                WaitForExit(pwshProcess).WaitOne();
                // Ensure all async operations are complete before disposing resources.
                waitHandle.SetResult(true);
                waitHandle.Task.ContinueWith((_) => DisposeResources());
            }).Start();
        }
        
        // Sends input to the PTY input pipe
        public void CopyInputToPipe(string command, bool noNewline = false)
        {
            var writer = new StreamWriter(new FileStream(inputPipe!.WriteSide, FileAccess.Write));
            
            var eol = noNewline ? "" : "\r";
            writer.Write(command + eol);

            writer.Flush();
        }
        
        // Read the PTY output pipe, writes it to event handler for processing, and forwards it
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;

            var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read);
            while ((bytesRead = await pseudoConsoleOutput.ReadAsync(byteBuffer.AsMemory(0, bufferLength))) > 0)
            {
                while (readPaused)
                    await Task.Delay(500);
                
                terminalBuffer.Append(byteBuffer.AsMemory(0, bytesRead));
                
                if (observerOutput != null)
                    await observerOutput.WriteAsync(byteBuffer.AsMemory(0, bytesRead));
                
                string outputChunk = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                
                // TODO: Remove logger before release
                logger.Log(outputChunk);
                OnOutputReceived(outputChunk);
            }
        }
        
        protected virtual void OnOutputReceived(string outputChunk)
        {
            var handler = HandleOutput;
            handler?.Invoke(this, outputChunk);
        }
        
        private static void OnClose(Action handler)
        {
            SetConsoleCtrlHandler(eventType =>
            {
                if(eventType == CtrlTypes.CTRL_CLOSE_EVENT)
                    handler();
                
                return false;
            }, true);
        }
        
        public void DisposeResources()
        {
            if (disposed)
                return;
            
            disposed = true;
            
            Console.ResetColor();
            
            observerMutex?.ReleaseMutex();
            
            ptyProcess?.Dispose();
            pwshProcess?.Dispose();
            outputPipe?.Dispose();
            inputPipe?.Dispose();
        }
        
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
        
        private static string GenerateSessionId() => $"PE-{Guid.NewGuid()}-{RandomNumberGenerator.GetInt32(100000)}";

        public void CreateObserver(bool isInteractive,  bool noNewWindow)
        {
            readPaused = true;
            
            terminalBuffer.Flush();
            
            observerProcess = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    FileName = "pwsh.exe",
                    Arguments = $"-NoExit -ExecutionPolicy Bypass -Command {GetObserverScript()}",
                    UseShellExecute = !noNewWindow,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false
                }
            };
            
            observerProcess.Start();
            
            // This mutex will signal to the observer process that the main process has been destroyed, and it may take over its own I/O
            observerMutex = new Mutex(true, observerMutexName);
            
            EnumWindows((hWnd, lParam) =>
            {
                int currentProcessId;
                GetWindowThreadProcessId(hWnd, out currentProcessId);

                if (currentProcessId != observerProcess.Id || !IsWindowVisible(hWnd)) return true;
                
                observerWindowHandle = hWnd;
                return false;
            }, IntPtr.Zero);
            
            // Create a named pipe server to send PTY output to the observer terminal
            observerOutput = new NamedPipeServerStream(observerOutputPipeName, PipeDirection.Out, 1 , PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            observerOutput.WaitForConnection();
            observerOutput.Write(Encoding.UTF8.GetBytes(terminalBuffer.ReadLastLines()));
            
            observerResize = new NamedPipeServerStream(observerResizePipeName, PipeDirection.In );
            observerResize.WaitForConnection();
            Task.Run(ObserverResize);

            if (isInteractive)
            {
                observerInteractive = true;
                Task.Run(ObserverInput);
            }
            
            readPaused = false;
        }

        private string GetObserverScript() => $@"
            Import-Module '{dllPath}'

            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8

            $observer = New-Object PowershellExpectDriver.ObserverInternals('{observerOutputPipeName}', '{observerInputPipeName}', '{observerResizePipeName}', '{observerMutexName}')
        ";
        
        public static void BringWindowToFront(IntPtr hwnd)
        {
            ShowWindow(hwnd, SW_RESTORE);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }

        private void ResizeTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!resizeTriggered) return;
            ResizePTY(ptyWidth, ptyHeight);
            resizeTriggered = false;
        }
        
        private void ResizePTY(int newWidth, int newHeight)
        {
            Console.WriteLine($"Resizing observer terminal to {newWidth}x{newHeight}");
            readPaused = true;
            observerInteractive = false;
            ptyProcess?.Resize(newWidth, newHeight);
            readPaused = false;
            observerInteractive = true;
        }

        public async Task ObserverResize()
        {
            // Initialize the resize timer but don't start it yet.
            // Timer will call ResizePty method after 1000ms of inactivity.
            resizeTimer = new System.Timers.Timer(1000);
            resizeTimer.AutoReset = false;
            resizeTimer.Elapsed += ResizeTimerElapsed;
            
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;

            while ((bytesRead = await observerResize.ReadAsync(byteBuffer, 0, bufferLength)) > 0)
            {
                var resizeMessage = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                var dimensions = resizeMessage.Split('x');
                var newWidth = int.Parse(dimensions[0]);
                var newHeight = int.Parse(dimensions[1]);

                if (newHeight == ptyHeight && newWidth == ptyWidth) continue;

                ptyWidth = newWidth;
                ptyHeight = newHeight;

                resizeTriggered = true;
                resizeTimer.Stop(); // Stop the timer if it's already running
                resizeTimer.Start(); // Start or restart the timer
            }
        }
        
        // Reads input from the observer terminal and writes it to the PTY input pipe
        public async void ObserverInput()
        {
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;
            observerInput = new NamedPipeServerStream(observerInputPipeName, PipeDirection.In);

            await observerInput.WaitForConnectionAsync();
            while ((bytesRead = await observerInput.ReadAsync(byteBuffer, 0, bufferLength)) > 0)
            {
                if (!observerInteractive) continue;
                var outputChunk = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                CopyInputToPipe(outputChunk, true);
            }
        }
        
        public void FocusObserver(bool isInteractive)
        {
            if (observerProcess == null)
                return;

            observerInteractive = isInteractive;
            
            if (observerWindowHandle != IntPtr.Zero)
            {
                BringWindowToFront(observerWindowHandle);
            }
        }
        
        public void DestroyObserver() {
            readPaused = true;
            
            ResizePTY(initialPtyWidth, initialPtyHeight);
            
            observerMutex?.Dispose();
            observerMutex = null;
            observerOutput?.Dispose();
            observerOutput = null;
            observerInput?.Dispose();
            observerInput = null;
            observerResize?.Dispose();
            observerResize = null;
            observerProcess?.Kill();
            observerProcess = null;
            
            readPaused = false;
        }
    }
}