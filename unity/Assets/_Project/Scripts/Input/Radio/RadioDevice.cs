using System;
using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>One value control of a HID device: its usage page and usage, and its logical range.</summary>
    public readonly struct RadioAxis
    {
        public readonly ushort Page, Usage;
        public readonly int Min, Max;

        public RadioAxis(ushort page, ushort usage, int min, int max)
        {
            Page = page;
            Usage = usage;
            Min = min;
            Max = max;
        }
    }

    /// <summary>One input report at the device boundary: the HID's logical values, stamped on arrival.</summary>
    public sealed class RadioReport
    {
        /// <summary>HostClock µs when the report reached the input thread; for a loss, when the loss was seen.</summary>
        public long HostUs;

        /// <summary>Each value in its logical range, in the descriptor's order: a TX12's channels 1–8.</summary>
        public readonly int[] Axes;

        /// <summary>Button i is bit i: a TX12's channels 9–32, set while the channel is above centre.</summary>
        public ulong Buttons;

        public RadioReport(int axes) => Axes = new int[axes];

        public void CopyTo(RadioReport to)
        {
            to.HostUs = HostUs;
            to.Buttons = Buttons;
            Array.Copy(Axes, to.Axes, Math.Min(Axes.Length, to.Axes.Length));
        }
    }

    public enum RadioRead
    {
        /// <summary>A report arrived.</summary>
        Report,

        /// <summary>None within the timeout, or one without any stick in it.</summary>
        Timeout,

        /// <summary>The device is gone (the USB cable pulled): the report's HostUs is when the loss was seen.</summary>
        Lost,
    }

    /// <summary>
    /// A stick device at the boundary the input thread reads (design 1): a live HID device (WinHidDevice) or a recorded
    /// stream (RecordedRadio). The device stamps each report the moment it hands it over, so stamping is on arrival.
    /// </summary>
    public interface IRadioDevice : IDisposable
    {
        /// <summary>The product name the device reports (never its path or serial number).</summary>
        string Name { get; }

        InputSource Source { get; }

        RadioAxis[] Axes { get; }

        int ButtonCount { get; }

        /// <summary>Waits up to timeoutMs for the next report and decodes it into report, which has Axes.Length axes.</summary>
        RadioRead Read(RadioReport report, int timeoutMs);
    }
}
