using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    public class PTY
    {
        public event EventHandler<string>? OutputReceived;
        
        private PTYPipe? inputPipe;
        private PTYPipe? outputPipe;
        private PTYHandler? ptyProcess;
        private Process? pwshProcess;
        private TerminalBuffer terminalBuffer = new();
        private bool readPaused = false;
        
        private System.Diagnostics.Process? observerProcess;
        private string observerId = "";
        private string observerOutputPipeName = "output";
        private string observerInputPipeName = "input";
        private System.IO.Pipes.NamedPipeServerStream? observerOutput;
        private System.IO.Pipes.NamedPipeServerStream? observerInput;
        private IntPtr observerHandle = IntPtr.Zero;

        public void Run()
        {
            inputPipe = new PTYPipe();
            outputPipe = new PTYPipe();
            ptyProcess = PTYHandler.Create(inputPipe.ReadSide, outputPipe.WriteSide, 120, 30);
            pwshProcess = ProcessFactory.Start(PTYHandler.PseudoConsoleThreadAttribute, ptyProcess.Handle);
            
            Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
            
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
                waitHandle.Task.ContinueWith((_) =>
                {
                    DisposeResources();
                });
            }).Start();
        }
        
        public async void ObserverInput()
        {
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;
            observerInput = new NamedPipeServerStream(observerInputPipeName, PipeDirection.In);

            await observerInput.WaitForConnectionAsync();
            while ((bytesRead = await observerInput.ReadAsync(byteBuffer, 0, bufferLength)) > 0)
            {
                string outputChunk = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                CopyInputToPipe(outputChunk, true);
            }
        }
        
        public void CopyInputToPipe(string command, bool noNewline = false)
        {
            var writer = new StreamWriter(new FileStream(inputPipe!.WriteSide, FileAccess.Write));
            
            if (noNewline)
            {
                writer.Write(command);
            }
            else
            {
                writer.WriteLine(command);
            }
            writer.Flush();
        }
        
        static string GenerateRandomHash()
        {
            return Guid.NewGuid().ToString();
        }

        static int GenerateRandomNumber()
        {
            return RandomNumberGenerator.GetInt32(100000);
        }
        
        static string GenerateUniqueWindowTitle()
        {
            return "PowerShellExpect-" + GenerateRandomHash() + GenerateRandomNumber();
        }
        
        public void ShowObserver()
        {
            ShowWindow(observerHandle, 5);
        }
        
        public void HideObserver()
        {
            ShowWindow(observerHandle, 0);
        }

        public void CreateObserver(string dllPath)
        {
            readPaused = true;
            
            terminalBuffer.Flush();
            
            observerProcess = new System.Diagnostics.Process();

            // Generate a unique window title to identify the observer terminal
            observerId = GenerateUniqueWindowTitle();
            observerOutputPipeName = observerId + "output";
            observerInputPipeName = observerId + "input";

            // Set window size to match PTY, set window title, set output encoding to UTF-8, and initialize the named pipe server
            string scriptContent = $@"
                Import-Module '{dllPath}'
                
                $Host.UI.RawUI.WindowSize.Height = 30
                $Host.UI.RawUI.WindowSize.Width = 120
                $Host.UI.RawUI.BufferSize.Height = 30
                $Host.UI.RawUI.BufferSize.Width = 120

                [Console]::OutputEncoding = [System.Text.Encoding]::UTF8

                Get-Content -Path '{terminalBuffer.Path}' 

                $observer = New-Object PowershellExpectDriver.ObserverTerminal

                $observer.Initialize('{observerOutputPipeName}', '{observerInputPipeName}')
            ";
            
            observerProcess.StartInfo.FileName = "pwsh.exe";
            observerProcess.StartInfo.Arguments = $"-NoExit -ExecutionPolicy Bypass -Command {scriptContent}";
            observerProcess.StartInfo.UseShellExecute = true;
            observerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            observerProcess.StartInfo.CreateNoWindow = false;
            
            observerProcess.Start();
            
            // Create a named pipe server to send PTY output to the observer terminal
            observerOutput = new NamedPipeServerStream(observerOutputPipeName, PipeDirection.Out, 1 , PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            observerOutput.WaitForConnection();
            
            Task.Run(ObserverInput);
            
            // TODO: Instead of using sleep, maybe check to see if output and input pipes are connected, then resume output
            // Ensure the terminal has time to initialize before resuming output
            Thread.Sleep(2000);
            
            readPaused = false;
        }
        
        // Read the child process output and write it to an event handler for processing
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            const int bufferLength = 4096;
            var byteBuffer = new byte[bufferLength];
            int bytesRead;

            var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read);
            
            while ((bytesRead = await pseudoConsoleOutput.ReadAsync(byteBuffer.AsMemory(0, bufferLength))) > 0)
            {
                while (readPaused)
                {
                    Thread.Sleep(500);
                }
                
                terminalBuffer.Append(byteBuffer.AsMemory(0, bytesRead));
                
                if (observerOutput != null)
                {
                    await observerOutput.WriteAsync(byteBuffer.AsMemory(0, bytesRead));
                }
                
                string outputChunk = Encoding.UTF8.GetString(byteBuffer, 0, bytesRead);
                OnOutputReceived(outputChunk);
            }
        }
        
        protected virtual void OnOutputReceived(string outputChunk)
        {
            var handler = OutputReceived;
            handler?.Invoke(this, outputChunk);
        }
        
        private static void OnClose(Action handler)
        {
            SetConsoleCtrlHandler(eventType =>
            {
                if(eventType == CtrlTypes.CTRL_CLOSE_EVENT)
                {
                    handler();
                }
                return false;
            }, true);
        }
        
        public void DisposeResources()
        {
            observerOutput?.Close();
            observerInput?.Close();
            observerProcess?.Close();
            
            Console.ResetColor();
            
            var disposables = new List<IDisposable?> { ptyProcess, pwshProcess, outputPipe, inputPipe };
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }
        
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
    }
    
    internal sealed class PTYPipe : IDisposable
    {
        public readonly SafeFileHandle ReadSide;
        public readonly SafeFileHandle WriteSide;

        public PTYPipe()
        {
            if (!CreatePipe(out ReadSide, out WriteSide, IntPtr.Zero, 0))
            {
                throw new InvalidOperationException("failed to create pipe");
            }
        }

        #region IDisposable

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            ReadSide.Dispose();
            WriteSide.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
    
    internal sealed class PTYHandler : IDisposable 
    {
        public static readonly IntPtr PseudoConsoleThreadAttribute = (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE;

        public IntPtr Handle { get; }

        private PTYHandler(IntPtr handle)
        {
            this.Handle = handle;
        }

        internal static PTYHandler Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, int width, int height)
        {
            var createResult = CreatePseudoConsole(
                new COORD {
                    X = (short)width, 
                    Y = (short)height
                },
                inputReadSide, 
                outputWriteSide,
                0, 
                out IntPtr hPC
            );
            if(createResult != 0)
            {
                throw new InvalidOperationException("Could not create pseudo console. Error Code " + createResult);
            }
            return new PTYHandler(hPC);
        }

        public void Dispose()
        {
            ClosePseudoConsole(Handle);
        }
    }
    
    public class ObserverTerminal
    {
        private string observerOutputPipeName = "output";
        private string observerInputPipeName = "input";
        
        // VT sequence constants
        private const string CSI = "\x1B[";
        private const string ESC = "\x1B";
        private const string SHIFT = "1;2"; 
        private const string ALT = "1;3"; 
        private const string SHIFT_ALT = "1;4"; 
        private const string CTRL = "1;5";
        private const string SHIFT_CTRL = "1;6";
        private const string ALT_CTRL = "1;7";
        private const string SHIFT_ALT_CTRL = "1;8";
        
        
        public void Initialize(string outputPipeName, string inputPipeName)
        {
            observerOutputPipeName = outputPipeName;
            observerInputPipeName = inputPipeName;
            
            Task.Run(ReadChildOutput);
            InputInterceptor();
        }
        
        private string TranslateKeyToVTSequence(ConsoleKeyInfo keyInfo)
        {
            var modifier = keyInfo.Modifiers switch
            {
                ConsoleModifiers.Alt | ConsoleModifiers.Control => ALT_CTRL,
                ConsoleModifiers.Shift | ConsoleModifiers.Control => SHIFT_CTRL,
                ConsoleModifiers.Shift | ConsoleModifiers.Alt => SHIFT_ALT,
                ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control => SHIFT_ALT_CTRL,
                ConsoleModifiers.Control => CTRL,
                ConsoleModifiers.Alt => ALT,
                ConsoleModifiers.Shift => SHIFT,
                _ => ""
            };
            
            return keyInfo.Key switch
            {
                ConsoleKey.UpArrow => CSI + modifier + "A",
                ConsoleKey.DownArrow => CSI + modifier + "B",
                ConsoleKey.RightArrow => CSI + modifier + "C",
                ConsoleKey.LeftArrow => CSI + modifier + "D",
                
                ConsoleKey.Home => CSI + modifier + "H",
                ConsoleKey.End => CSI + modifier + "F",
                ConsoleKey.PageUp => CSI + modifier + "5~",
                ConsoleKey.PageDown => CSI + modifier + "6~",
                ConsoleKey.Insert => CSI + modifier + "2~",
                ConsoleKey.Delete => CSI + modifier + "3~",
                
                ConsoleKey.F1 => ESC + "OP",
                ConsoleKey.F2 => ESC + "OQ",
                ConsoleKey.F3 => ESC + "OR",
                ConsoleKey.F4 => ESC + "OS",
                ConsoleKey.F5 => CSI + "15~",
                ConsoleKey.F6 => CSI + "17~",
                ConsoleKey.F7 => CSI + "18~",
                ConsoleKey.F8 => CSI + "19~",
                ConsoleKey.F9 => CSI + "20~",
                ConsoleKey.F10 => CSI + "21~",
                ConsoleKey.F11 => CSI + "23~",
                ConsoleKey.F12 => CSI + "24~",
                
                ConsoleKey.Escape => "\x1b",
                ConsoleKey.Enter => "\x0d",
                ConsoleKey.Tab => "\x09",
                ConsoleKey.Backspace => "\x7f",
                
                _ => keyInfo.KeyChar.ToString()
            };
        }

        // Intercepts input from the observer terminal and sends it to the PTY
        private void InputInterceptor()
        {
            var pipeClient = new NamedPipeClientStream(".", observerInputPipeName, PipeDirection.Out);
            pipeClient.Connect();
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                SendKeyToChild(keyInfo, pipeClient);
            }
        }

        // Translates a ConsoleKeyInfo to a VT sequence and sends it to the PTY
        private void SendKeyToChild(ConsoleKeyInfo keyInfo, NamedPipeClientStream pipeClient)
        {
            var vt = TranslateKeyToVTSequence(keyInfo);
            byte[] vtBytes = Encoding.UTF8.GetBytes(vt);
            pipeClient.Write(vtBytes, 0, vtBytes.Length);
            pipeClient.Flush();
        }
        
        // Receives output sent from the PTY and writes it to the observer terminal
        private async Task ReadChildOutput()
        {
            var pipeClient = new NamedPipeClientStream(".", observerOutputPipeName, PipeDirection.In);
            await pipeClient.ConnectAsync();
            var terminalOutput = Console.OpenStandardOutput();
            
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = pipeClient.Read(buffer, 0, buffer.Length)) > 0)
            {
                await terminalOutput.WriteAsync(buffer, 0, bytesRead);
            }
        }
    }
}