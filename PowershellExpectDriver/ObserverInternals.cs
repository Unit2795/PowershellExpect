using System.IO.Pipes;
using System.Text;

namespace PowershellExpectDriver
{
    // Attached directly to the observer terminal powershell process
    public class ObserverInternals
    {
        private string observerOutputPipeName;
        private string observerInputPipeName;
        private string observerResizePipeName;
        private string observerMutexName;
        private Mutex observerMutex;
        private NamedPipeClientStream? outputPipeClient;
        private NamedPipeClientStream? inputPipeClient;
        private NamedPipeClientStream? resizePipeClient;
        private CancellationTokenSource cancellationTokenSource;
        private long lastAdjustment;
        private int observerWidth;
        private int observerHeight;
        
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
        
        public ObserverInternals(string outputPipeName, string inputPipeName, string resizePipeName,string mutexName)
        {
            observerOutputPipeName = outputPipeName;
            observerInputPipeName = inputPipeName;
            observerResizePipeName = resizePipeName;
            inputPipeClient = new NamedPipeClientStream(".", observerInputPipeName, PipeDirection.Out);
            outputPipeClient  = new NamedPipeClientStream(".", observerOutputPipeName, PipeDirection.In);
            resizePipeClient = new NamedPipeClientStream(".", observerResizePipeName, PipeDirection.Out);
            observerMutexName = mutexName;
            
            // Open the existing named Mutex
            observerMutex = Mutex.OpenExisting(observerMutexName);
            
            resizePipeClient.Connect();
            observerWidth = Console.WindowWidth;
            observerHeight = Console.WindowHeight;
            ResizeObserver();
            
            cancellationTokenSource = new CancellationTokenSource();
            
            Task.Run(() => PrintOutputToObserver(cancellationTokenSource.Token));
            Task.Run(() => InputInterceptor(cancellationTokenSource.Token));
            Task.Run(() => MonitorResize(cancellationTokenSource.Token));
            
            observerMutex.WaitOne();
            
            cancellationTokenSource.Cancel();
            DetachPipes();
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
        private void InputInterceptor(CancellationToken cancellationToken)
        {
            try
            {
                inputPipeClient.Connect();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!inputPipeClient.IsConnected)
                        break;

                    // Check Console.KeyAvailable to avoid blocking the loop.
                    if (!Console.KeyAvailable) continue;
                    var keyInfo = Console.ReadKey(true);
                    SendKeyToChild(keyInfo, inputPipeClient);
                }
            }
            catch (IOException)
            {
                // Pipe closed, silently exit the method
                return;
            }
        }

        // Translates a ConsoleKeyInfo to a VT sequence and sends it to the PTY
        private void SendKeyToChild(ConsoleKeyInfo keyInfo, NamedPipeClientStream pipeClient)
        {
            var vt = TranslateKeyToVTSequence(keyInfo);
            byte[] vtBytes = Encoding.UTF8.GetBytes(vt);
            try
            {
                pipeClient.Write(vtBytes, 0, vtBytes.Length);
                pipeClient.Flush();
            }
            catch (IOException)
            {
                // Pipe closed, silently exit the method
                return;
            }
        }
        
        // Receives output sent from the PTY and writes it to the observer terminal
        private async Task PrintOutputToObserver(CancellationToken cancellationToken)
        {
            try
            {
                await outputPipeClient.ConnectAsync();
                var terminalOutput = Console.OpenStandardOutput();
                var buffer = new byte[4096];
                int bytesRead;

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!outputPipeClient.IsConnected)
                        break;
                    
                    bytesRead = await outputPipeClient.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        await terminalOutput.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    }
                }
            }
            catch (IOException)
            {
                // Pipe closed, silently exit the method
                return;
            }
        }
        
        private void MonitorResize(CancellationToken cancellationToken)
        {
            while (true)
            {
                /*
                    Adjust the PTY buffer size (if necessary, due to user resize) at max every 100ms to keep in sync with the PTY, to avoid the output being mangled.
                    NOTE: If this proves to be too inefficient, may need to investigate subclassing the terminal/receiving resize events, increase the interval, or try something else.
                */
                if (Console.WindowHeight != observerHeight || Console.WindowWidth != observerWidth)
                {
                    observerWidth = Console.WindowWidth;
                    observerHeight = Console.WindowHeight;
                    ResizeObserver();
                }
                
                Thread.Sleep(250);
            }
        }
        
        private void ResizeObserver()
        {
            var resizeMessage = Encoding.UTF8.GetBytes(observerWidth + "x" + observerHeight);
            resizePipeClient.Write(resizeMessage, 0, resizeMessage.Length);
        }
        
        private void DetachPipes()
        {
            outputPipeClient?.Close();
            inputPipeClient?.Close();

            // Terminal is now free to act on its own
        }
    }
}