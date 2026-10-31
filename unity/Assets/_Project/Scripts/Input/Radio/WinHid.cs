using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Microsoft.Win32.SafeHandles;

namespace Fpv450.Input.Radio
{
    /// <summary>A HID joystick or gamepad present on this machine (WinHid.List).</summary>
    public sealed class WinHidInfo
    {
        /// <summary>The device interface path, for opening only: never logged or shown (LOG-05).</summary>
        public string Path;

        public string Name;
        public ushort VendorId, ProductId;
        public InputSource Source;
    }

    /// <summary>
    /// Finds and opens HID joysticks and gamepads through Windows' HID class driver (INP-01). An EdgeTX or OpenTX radio
    /// in USB joystick mode is a Radio, any other a Joystick.
    /// </summary>
    public static class WinHid
    {
        /// <summary>
        /// The present joysticks and gamepads (HID usage page 1, usage 4, 5 or 8), radios first, else in Windows' order.
        /// Empty off Windows.
        /// </summary>
        public static List<WinHidInfo> List()
        {
            var radios = new List<WinHidInfo>();
            var others = new List<WinHidInfo>();
            string[] paths;
            try
            {
                paths = InterfacePaths();
            }
            catch (DllNotFoundException)
            {
                return radios;
            }
            catch (EntryPointNotFoundException)
            {
                return radios;
            }
            foreach (string path in paths)
            {
                using (SafeFileHandle h = Native.CreateFileW(path, 0, Native.ShareReadWrite, IntPtr.Zero, Native.OpenExisting, 0, IntPtr.Zero))
                {
                    if (h.IsInvalid || !Native.HidD_GetPreparsedData(h, out IntPtr pp)) continue;
                    bool stick;
                    try
                    {
                        stick = Native.HidP_GetCaps(pp, out HidpCaps caps) == Native.HidpSuccess && caps.UsagePage == 0x01
                            && (caps.Usage == 0x04 || caps.Usage == 0x05 || caps.Usage == 0x08);
                    }
                    finally
                    {
                        Native.HidD_FreePreparsedData(pp);
                    }
                    if (!stick) continue;
                    var a = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
                    Native.HidD_GetAttributes(h, ref a);
                    var info = new WinHidInfo
                    {
                        Path = path,
                        Name = ProductName(h),
                        VendorId = a.VendorId,
                        ProductId = a.ProductId,
                        Source = IsRadio(a.VendorId, a.ProductId) ? InputSource.Radio : InputSource.Joystick,
                    };
                    (info.Source == InputSource.Radio ? radios : others).Add(info);
                }
            }
            radios.AddRange(others);
            return radios;
        }

        /// <summary>The device for reading, or null when it cannot be opened.</summary>
        public static WinHidDevice Open(WinHidInfo info, IHostClock clock)
        {
            SafeFileHandle h = Native.CreateFileW(info.Path, Native.GenericRead, Native.ShareReadWrite, IntPtr.Zero, Native.OpenExisting,
                Native.FileFlagOverlapped, IntPtr.Zero);
            if (h.IsInvalid || !Native.HidD_GetPreparsedData(h, out IntPtr pp))
            {
                h.Dispose();
                return null;
            }
            try
            {
                return new WinHidDevice(info, clock, h, pp);
            }
            catch (InvalidOperationException)
            {
                Native.HidD_FreePreparsedData(pp);
                h.Dispose();
                return null;
            }
        }

        /// <summary>The first present device that opens (a radio before any other joystick), or null.</summary>
        public static WinHidDevice OpenFirst(IHostClock clock)
        {
            foreach (WinHidInfo info in List())
            {
                WinHidDevice d = Open(info, clock);
                if (d != null) return d;
            }
            return null;
        }

        // EdgeTX and OpenTX in USB joystick mode: pid.codes' 1209:4F54, and STMicroelectronics' 0483:5710 on older OpenTX.
        static bool IsRadio(ushort vid, ushort pid) => (vid == 0x1209 && pid == 0x4F54) || (vid == 0x0483 && pid == 0x5710);

        static string[] InterfacePaths()
        {
            Native.HidD_GetHidGuid(out Guid hid);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (Native.CM_Get_Device_Interface_List_SizeW(out int chars, ref hid, IntPtr.Zero, 0) != 0) break;
                var buffer = new byte[chars * 2];
                int cr = Native.CM_Get_Device_Interface_ListW(ref hid, IntPtr.Zero, buffer, chars, 0);
                if (cr == Native.CrBufferSmall) continue; // a device arrived between the two calls
                if (cr != 0) break;
                return Encoding.Unicode.GetString(buffer).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return new string[0];
        }

        static string ProductName(SafeFileHandle h)
        {
            var b = new byte[254];
            if (!Native.HidD_GetProductString(h, b, b.Length)) return "HID joystick";
            string s = Encoding.Unicode.GetString(b);
            int end = s.IndexOf('\0');
            s = (end < 0 ? s : s.Substring(0, end)).Trim();
            return s.Length > 0 ? s : "HID joystick";
        }
    }

    /// <summary>
    /// A HID joystick read through Windows' HID class driver (INP-01, INP-04). Read issues an overlapped ReadFile and
    /// waits on its event, so the input thread sleeps until a report arrives and stamps it with the host clock the
    /// moment the wait returns, before decoding it. The report is decoded with HidP_GetData by the device's own
    /// descriptor: the value controls except a hat switch, then the buttons, each in the order of its place in the
    /// report (a TX12's channels 1–8, then 9–32). The place is found by setting each control alone with HidP_SetData,
    /// because Windows numbers the data indices of one main item backwards (measured on a mouse: X at bit 16 has index
    /// 6, Y at bit 32 index 5), and EdgeTX declares its eight axes in one main item. A failed read is the device's loss
    /// (a pulled cable). Every call, Dispose included, belongs to the input thread. Steady state allocates nothing.
    /// </summary>
    public sealed class WinHidDevice : IRadioDevice
    {
        readonly IHostClock _clock;
        readonly SafeFileHandle _handle;
        readonly IntPtr _preparsed, _buffer, _overlapped, _event;
        readonly int _length;
        readonly HidpData[] _data;
        readonly int[] _slot; // by data index: an axis (≥ 0), a button (−2 − its bit) or nothing (−1)
        readonly int[] _signBits; // by axis: its bit size when its range is signed, else 0
        bool _pending, _lost, _disposed;

        internal WinHidDevice(WinHidInfo info, IHostClock clock, SafeFileHandle handle, IntPtr preparsed)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _handle = handle;
            _preparsed = preparsed;
            Name = info.Name;
            Source = info.Source;
            if (Native.HidP_GetCaps(preparsed, out HidpCaps caps) != Native.HidpSuccess) throw new InvalidOperationException("no HID caps");
            _length = caps.InputReportByteLength;

            var values = new HidpValueCaps[caps.NumberInputValueCaps];
            ushort nv = (ushort)values.Length;
            if (nv > 0 && Native.HidP_GetValueCaps(Native.HidpInput, values, ref nv, preparsed) != Native.HidpSuccess)
                throw new InvalidOperationException("no HID value caps");
            var probe = new byte[Math.Max(1, (int)_length)];
            var one = new HidpData[1];
            var axes = new List<(long At, int Index, RadioAxis Axis, int SignBits)>();
            for (int i = 0; i < nv; i++)
            {
                HidpValueCaps v = values[i];
                int count = v.IsRange != 0 ? v.UsageMax - v.UsageMin + 1 : 1;
                for (int j = 0; j < count; j++)
                {
                    ushort usage = (ushort)(v.UsageMin + j);
                    if (v.UsagePage == 0x01 && usage == 0x39) continue; // a hat switch is not an axis
                    long at = PlaceOf(preparsed, probe, one, v.DataIndexMin + j, v.ReportId);
                    if (at < 0) continue; // a value array, not a stick
                    int min = v.LogicalMin, max = v.LogicalMax;
                    if (min >= 0 && max < min && v.BitSize < 32) max = (int)((1L << v.BitSize) - 1); // an unsigned range read as signed
                    axes.Add((at, v.DataIndexMin + j, new RadioAxis(v.UsagePage, usage, min, max), min < 0 ? v.BitSize : 0));
                }
            }
            axes.Sort((a, b) => a.At.CompareTo(b.At));

            var buttonCaps = new HidpButtonCaps[caps.NumberInputButtonCaps];
            ushort nb = (ushort)buttonCaps.Length;
            if (nb > 0 && Native.HidP_GetButtonCaps(Native.HidpInput, buttonCaps, ref nb, preparsed) != Native.HidpSuccess)
                throw new InvalidOperationException("no HID button caps");
            var buttons = new List<(long At, int Index)>();
            for (int i = 0; i < nb; i++)
            {
                HidpButtonCaps b = buttonCaps[i];
                int count = b.IsRange != 0 ? b.DataIndexMax - b.DataIndexMin + 1 : 1;
                for (int j = 0; j < count; j++)
                {
                    long at = PlaceOf(preparsed, probe, one, b.DataIndexMin + j, b.ReportId);
                    if (at >= 0) buttons.Add((at, b.DataIndexMin + j));
                }
            }
            buttons.Sort((a, b) => a.At.CompareTo(b.At));

            _slot = new int[caps.NumberInputDataIndices];
            for (int i = 0; i < _slot.Length; i++) _slot[i] = -1;
            Axes = new RadioAxis[axes.Count];
            _signBits = new int[axes.Count];
            for (int i = 0; i < axes.Count; i++)
            {
                Axes[i] = axes[i].Axis;
                _signBits[i] = axes[i].SignBits;
                if (axes[i].Index < _slot.Length) _slot[axes[i].Index] = i;
            }
            ButtonCount = Math.Min(buttons.Count, 64);
            for (int k = 0; k < ButtonCount; k++)
                if (buttons[k].Index < _slot.Length)
                    _slot[buttons[k].Index] = -2 - k;

            _data = new HidpData[Math.Max(1, Native.HidP_MaxDataListLength(Native.HidpInput, preparsed))];
            _buffer = Marshal.AllocHGlobal(Math.Max(1, _length));
            _overlapped = Marshal.AllocHGlobal(Native.OverlappedSize);
            for (int i = 0; i < Native.OverlappedSize; i++) Marshal.WriteByte(_overlapped, i, 0);
            _event = Native.CreateEventW(IntPtr.Zero, true, false, IntPtr.Zero);
            Marshal.WriteIntPtr(_overlapped, Native.OverlappedEventOffset, _event);
        }

        public string Name { get; }
        public InputSource Source { get; }
        public RadioAxis[] Axes { get; }
        public int ButtonCount { get; }

        // A control's place in its report: its report id, then the first bit HidP_SetData sets for it alone; −1 when it
        // cannot be set alone.
        static long PlaceOf(IntPtr preparsed, byte[] probe, HidpData[] one, int index, byte reportId)
        {
            Array.Clear(probe, 0, probe.Length);
            probe[0] = reportId;
            one[0] = new HidpData { DataIndex = (ushort)index, RawValue = 1 };
            int n = 1;
            if (Native.HidP_SetData(Native.HidpInput, one, ref n, preparsed, probe, probe.Length) != Native.HidpSuccess) return -1;
            for (int bit = 8; bit < probe.Length * 8; bit++)
                if ((probe[bit >> 3] & (1 << (bit & 7))) != 0)
                    return ((long)reportId << 32) | (uint)bit;
            return -1;
        }

        public RadioRead Read(RadioReport report, int timeoutMs)
        {
            if (_lost) return Lose(report, _clock.NowUs());
            if (!_pending)
            {
                Native.ResetEvent(_event);
                if (!Native.ReadFile(_handle, _buffer, _length, IntPtr.Zero, _overlapped) && Marshal.GetLastWin32Error() != Native.ErrorIoPending)
                    return Lose(report, _clock.NowUs());
                _pending = true;
            }
            uint wait = Native.WaitForSingleObject(_event, (uint)Math.Max(0, timeoutMs));
            if (wait == Native.WaitTimeout) return RadioRead.Timeout;
            long arrived = _clock.NowUs(); // the arrival stamp, before anything else
            _pending = false;
            if (wait != Native.WaitObject0 || !Native.GetOverlappedResult(_handle, _overlapped, out int got, false))
                return Lose(report, arrived);
            report.HostUs = arrived;
            return Decode(report, got) ? RadioRead.Report : RadioRead.Timeout;
        }

        RadioRead Lose(RadioReport report, long nowUs)
        {
            _lost = true;
            report.HostUs = nowUs;
            return RadioRead.Lost;
        }

        bool Decode(RadioReport report, int length)
        {
            int n = _data.Length;
            if (Native.HidP_GetData(Native.HidpInput, _data, ref n, _preparsed, _buffer, length) != Native.HidpSuccess) return false;
            ulong buttons = 0;
            bool values = false;
            for (int i = 0; i < n; i++)
            {
                int index = _data[i].DataIndex;
                int slot = index < _slot.Length ? _slot[index] : -1;
                if (slot >= 0)
                {
                    int raw = (int)_data[i].RawValue;
                    int bits = _signBits[slot];
                    if (bits > 0 && bits < 32) raw = raw << (32 - bits) >> (32 - bits);
                    if (slot < report.Axes.Length) report.Axes[slot] = raw;
                    values = true;
                }
                else if (slot <= -2)
                {
                    buttons |= 1UL << (-2 - slot); // HidP_GetData lists only the buttons that are on
                }
            }
            report.Buttons = buttons;
            return values || Axes.Length == 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_pending)
            {
                Native.CancelIoEx(_handle, _overlapped);
                Native.GetOverlappedResult(_handle, _overlapped, out _, true); // the kernel is done with the buffers
                _pending = false;
            }
            _handle.Dispose();
            Native.CloseHandle(_event);
            Marshal.FreeHGlobal(_overlapped);
            Marshal.FreeHGlobal(_buffer);
            Native.HidD_FreePreparsedData(_preparsed);
        }
    }

#pragma warning disable 0649 // the fields below are filled by Windows

    [StructLayout(LayoutKind.Sequential)]
    struct HiddAttributes
    {
        public int Size;
        public ushort VendorId, ProductId, VersionNumber;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    struct HidpCaps
    {
        [FieldOffset(0)] public ushort Usage;
        [FieldOffset(2)] public ushort UsagePage;
        [FieldOffset(4)] public ushort InputReportByteLength;
        [FieldOffset(46)] public ushort NumberInputButtonCaps;
        [FieldOffset(48)] public ushort NumberInputValueCaps;
        [FieldOffset(50)] public ushort NumberInputDataIndices;
    }

    // HIDP_VALUE_CAPS: UsageMin and DataIndexMin share their offsets with NotRange's Usage and DataIndex.
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    struct HidpValueCaps
    {
        [FieldOffset(0)] public ushort UsagePage;
        [FieldOffset(2)] public byte ReportId;
        [FieldOffset(12)] public byte IsRange;
        [FieldOffset(18)] public ushort BitSize;
        [FieldOffset(40)] public int LogicalMin;
        [FieldOffset(44)] public int LogicalMax;
        [FieldOffset(56)] public ushort UsageMin;
        [FieldOffset(58)] public ushort UsageMax;
        [FieldOffset(68)] public ushort DataIndexMin;
        [FieldOffset(70)] public ushort DataIndexMax;
    }

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    struct HidpButtonCaps
    {
        [FieldOffset(0)] public ushort UsagePage;
        [FieldOffset(2)] public byte ReportId;
        [FieldOffset(12)] public byte IsRange;
        [FieldOffset(68)] public ushort DataIndexMin;
        [FieldOffset(70)] public ushort DataIndexMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HidpData
    {
        public ushort DataIndex;
        public ushort Reserved;
        public uint RawValue;
    }

#pragma warning restore 0649

    static class Native
    {
        public const int HidpInput = 0, HidpSuccess = 0x00110000, CrBufferSmall = 0x1A, ErrorIoPending = 997;
        public const uint GenericRead = 0x80000000, ShareReadWrite = 3, OpenExisting = 3, FileFlagOverlapped = 0x40000000;
        public const uint WaitObject0 = 0, WaitTimeout = 0x102;

        // OVERLAPPED: Internal, InternalHigh, Offset and OffsetHigh (or Pointer), hEvent.
        public static readonly int OverlappedEventOffset = 2 * IntPtr.Size + 8;
        public static readonly int OverlappedSize = 3 * IntPtr.Size + 8;

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HiddAttributes attributes);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr preparsed);

        [DllImport("hid.dll")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsed);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool HidD_GetProductString(SafeFileHandle h, [Out] byte[] buffer, int bytes);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr preparsed, out HidpCaps caps);

        [DllImport("hid.dll")]
        public static extern int HidP_GetValueCaps(int type, [In, Out] HidpValueCaps[] caps, ref ushort length, IntPtr preparsed);

        [DllImport("hid.dll")]
        public static extern int HidP_GetButtonCaps(int type, [In, Out] HidpButtonCaps[] caps, ref ushort length, IntPtr preparsed);

        [DllImport("hid.dll")]
        public static extern int HidP_MaxDataListLength(int type, IntPtr preparsed);

        [DllImport("hid.dll")]
        public static extern int HidP_SetData(int type, [In, Out] HidpData[] data, ref int length, IntPtr preparsed, [In, Out] byte[] report, int reportLength);

        [DllImport("hid.dll")]
        public static extern int HidP_GetData(int type, [In, Out] HidpData[] data, ref int length, IntPtr preparsed, IntPtr report, int reportLength);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Get_Device_Interface_List_SizeW(out int chars, ref Guid interfaceClass, IntPtr deviceId, int flags);

        [DllImport("cfgmgr32.dll")]
        public static extern int CM_Get_Device_Interface_ListW(ref Guid interfaceClass, IntPtr deviceId, [Out] byte[] buffer, int chars, int flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(SafeFileHandle h, IntPtr buffer, int bytes, IntPtr read, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(SafeFileHandle h, IntPtr overlapped, out int transferred, bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(SafeFileHandle h, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateEventW(IntPtr security, bool manualReset, bool initialState, IntPtr name);

        [DllImport("kernel32.dll")]
        public static extern bool ResetEvent(IntPtr h);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr h, uint milliseconds);
    }
}
