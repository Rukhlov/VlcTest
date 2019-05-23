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

    public class VideoUtils
    {

        public static int EstimateVideoSize(int width, int height, PixelFormat fmt)
        {
            int size = 0;
            if (fmt == PixelFormats.Bgra32 || fmt == PixelFormats.Bgr32)
            {
                var pitches = GetAlignedDimension((uint)(width * fmt.BitsPerPixel) / 8, 32);
                var lines = GetAlignedDimension((uint)height, 32);
                size = (int)(pitches * lines);
            }

            return size;
        }

        public static uint GetAlignedDimension(uint dimension, uint mod)
        {
            var modResult = dimension % mod;
            if (modResult == 0)
            {
                return dimension;
            }

            return dimension + mod - (dimension % mod);
        }


        public static PixelFormat ToPixelFormat(System.Drawing.Imaging.PixelFormat sourceFormat)
        {
            switch (sourceFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return PixelFormats.Bgr24;

                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    return PixelFormats.Bgra32;

                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    return PixelFormats.Bgr32;

            }
            return new PixelFormat();
        }

        public static System.Drawing.Imaging.PixelFormat ToGdiPixelFormat(PixelFormat sourceFormat)
        {
            System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Undefined;
            if (sourceFormat == PixelFormats.Bgr24)
            {
                format = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
            }
            else if (sourceFormat == PixelFormats.Bgra32)
            {
                format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            }
            else if (sourceFormat == PixelFormats.Bgr32)
            {
                format = System.Drawing.Imaging.PixelFormat.Format32bppRgb;
            }
            return format;
        }
    }

    public class SharedBuffer
    {
        public string Name { get; private set; }

        private string mutexName = "";
        private string memoryName = "";
        private string eventName = "";

        public SharedBuffer() { }

        public SharedBuffer(string name) : this(name, 10 * 1024 * 1024)
        { }

        public SharedBuffer(string name, long capacity)
        {
            this.Name = name;

            this.mutexName = Name + "-mutex";
            this.memoryName = Name + "-memory";
            this.eventName = Name + "-event";

            Create(capacity);
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


        private void Create(long capacity)
        {

            try
            {
                bool created = false;
                mutex = new Mutex(false, mutexName, out created);
                if (!created)
                {
                    //...
                }

                {//EventWaitHandle
                    var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                    var rights = EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify;
                    var rule = new EventWaitHandleAccessRule(users, rights, AccessControlType.Allow);

                    var security = new EventWaitHandleSecurity();
                    security.AddAccessRule(rule);
                    ewh = new EventWaitHandle(false, EventResetMode.AutoReset, eventName, out created);

                    if (!created)
                    {
                        //...
                    }
                }


                mmf = MemoryMappedFile.CreateNew(memoryName, capacity, MemoryMappedFileAccess.ReadWrite);
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

        public static bool TryOpenExisting(string name, out SharedBuffer buffer)
        {
            buffer = null;
            try
            {
                buffer = OpenExisting(name);

            }
            catch (Exception ex) { }

            return (buffer != null);

        }

        public static SharedBuffer OpenExisting(string name)
        {
            SharedBuffer buffer = null;

            Mutex _mutex = null;
            EventWaitHandle _ewh = null;
            MemoryMappedFile _mmf = null;
            MemoryMappedViewAccessor _mmva = null;
            try
            {
                var _mutexName = name + "-mutex";
                var _memoryName = name + "-memory";
                var _eventName = name + "-event";

                _mutex = Mutex.OpenExisting(_mutexName);

                var rights = EventWaitHandleRights.Synchronize | EventWaitHandleRights.Modify;
                _ewh = EventWaitHandle.OpenExisting(_eventName, rights);


                _mmf = MemoryMappedFile.OpenExisting(_memoryName);
                var _section = _mmf.SafeMemoryMappedFileHandle.DangerousGetHandle();

                _mmva = _mmf.CreateViewAccessor(0, 0);
                var _data = _mmva.SafeMemoryMappedViewHandle.DangerousGetHandle();

                buffer = new SharedBuffer
                {
                    Name = name,
                    mutexName = _mutexName,
                    memoryName = _memoryName,
                    eventName = _eventName,

                    mutex = _mutex,
                    ewh = _ewh,
                    mmf = _mmf,
                    mmva = _mmva,

                    Section = _section,
                    Data = _data,

                };
            }
            catch (Exception ex)
            {
                _mutex?.Dispose();
                _ewh?.Dispose();
                _mmf?.Dispose();
                _mmva?.Dispose();

                throw;
            }

            return buffer;

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
