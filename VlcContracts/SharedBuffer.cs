using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VlcContracts
{


    public enum VideoBufferState : byte
    {
        None = 0,
        Setup = 1,
        Display = 2,
        Cleanup = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VideoBufferInfo
    {
        public VideoBufferState State;

        public int Width;
        public int Height;
        public PixelFormat PixelFormat;
        public uint Pitches;

        public int DataOffset;
        public long DataLenght;


        public override string ToString()
        {
            return string.Join(" ", State, Width, Height, PixelFormat, Pitches, DataOffset, DataLenght);
        }
    }


    public class SharedBuffer
    {
        public readonly string Name = Guid.NewGuid().ToString("N");

        private readonly string mutexName = "";
        private readonly string memoryName = "";
        private readonly string eventName = "";

        public SharedBuffer(string name, long capacity)
        {
            this.Name = name;

            this.mutexName = Name + "-mutex";
            this.memoryName = Name + "-memory";
            this.eventName = Name + "-event";

            Construct(capacity);
        }

        private Mutex mutex = null;
        private EventWaitHandle ewh = null;
        private MemoryMappedFile mmf = null;
        private MemoryMappedViewAccessor mmva = null;


        public IntPtr Section { get; private set; } = IntPtr.Zero;
        public IntPtr Data { get; private set; } = IntPtr.Zero;

        public long Capacity
        {
            get
            {
                return mmva?.Capacity ?? -1;
            }
        }

        private void Construct(long capacity)
        {

            try
            {
                bool created = false;
                mutex = new Mutex(false, mutexName, out created);

                {//EventWaitHandle
                    var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                    var rights = EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify;
                    var rule = new EventWaitHandleAccessRule(users, rights, AccessControlType.Allow);

                    var security = new EventWaitHandleSecurity();
                    security.AddAccessRule(rule);
                    ewh = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out created);
                }


                mmf = MemoryMappedFile.CreateOrOpen(memoryName, capacity, MemoryMappedFileAccess.ReadWrite);
                Section = mmf.SafeMemoryMappedFileHandle.DangerousGetHandle();

                mmva = mmf.CreateViewAccessor(0, 0);
                Data = mmva.SafeMemoryMappedViewHandle.DangerousGetHandle();

            }
            catch (Exception ex)
            {
                Dispose();

                throw;
            }

        }


        public T ReadData<T>(long position = 0, int timeout = -1) where T : struct
        {
            T t = new T();
            if (mutex.WaitOne(timeout))
            {
                try
                {
                    mmva.Read<T>(position, out t);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            else
            {
                throw new TimeoutException();
            }

            return t;
        }

        public void WriteData<T>(T t, long position = 0, int timeout = -1) where T : struct
        {
            if (mutex.WaitOne(timeout))
            {
                try
                {
                    mmva.Write<T>(position, ref t);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            else
            {
                throw new TimeoutException();
            }
        }

        public void Pulse()
        {
            ewh?.Set();
        }

        public bool WaitForSignal(int timeout = -1)
        {
            return ewh?.WaitOne(timeout) ?? false;
        }


        private volatile bool mutexInUse = false;
        unsafe public IntPtr AcquirePointer(int timeout = -1)
        {
            IntPtr handle = IntPtr.Zero;
            if (!mutex.WaitOne(timeout))
            {
                throw new TimeoutException();
            }

            mutexInUse = true;
            byte* ptr = (byte*)0;
            mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            handle = (IntPtr)ptr;

            return handle;
        }

        public void ReleasePointer()
        {

            mmva.SafeMemoryMappedViewHandle.ReleasePointer();

            mutex.ReleaseMutex();
            mutexInUse = false;

        }

        public void Dispose()
        {
            mmf?.Dispose();
            mmva?.Dispose();

            if (mutexInUse)
            {
                mutex.ReleaseMutex();
            }

            mutex?.Dispose();

            ewh?.Dispose();
        }

    }
}
