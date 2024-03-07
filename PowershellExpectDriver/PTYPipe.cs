using Microsoft.Win32.SafeHandles;
using static PowershellExpectDriver.PInvoke;

namespace PowershellExpectDriver
{
    internal sealed class PTYPipe : IDisposable
    {
        public readonly SafeFileHandle ReadSide;
        public readonly SafeFileHandle WriteSide;

        public PTYPipe()
        {
            if (!CreatePipe(out ReadSide, out WriteSide, IntPtr.Zero, 0))
                throw new InvalidOperationException("failed to create pipe");
        }

        public void Dispose()
        {
            ReadSide.Dispose();
            WriteSide.Dispose();
        }
    }
}