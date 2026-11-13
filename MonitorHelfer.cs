using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Klangkiste
{
    /// <summary>
    /// Hilfsklasse, um das Fenster monitor-übergreifend korrekt wiederherzustellen.
    /// Arbeitet mit Win32-APIs (keine zusätzliche Referenz auf System.Windows.Forms nötig)
    /// und rechnet zwischen physischen Pixeln und WPF-Geräte-unabhängigen Einheiten (DIP) um.
    /// </summary>
    public static class MonitorHelfer
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        // Erweiterte Variante mit Gerätenamen (z. B. "\\.\DISPLAY2").
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfoEx(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        /// <summary>
        /// Arbeitsbereich (in DIP) des Monitors, auf dem das Fenster aktuell liegt,
        /// plus dessen Gerätename.
        /// </summary>
        public static (Rect arbeitsbereich, string geraet) MonitorVonFenster(Window fenster)
        {
            IntPtr hwnd = new WindowInteropHelper(fenster).Handle;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (monitor != IntPtr.Zero && GetMonitorInfoEx(monitor, ref info))
            {
                return (ToDip(fenster, info.rcWork), info.szDevice);
            }

            return (SystemParameters.WorkArea, string.Empty);
        }

        /// <summary>
        /// Sucht den Monitor mit dem angegebenen Gerätenamen und liefert dessen Arbeitsbereich
        /// (in DIP). Gibt false zurück, wenn dieser Monitor nicht (mehr) existiert.
        /// </summary>
        public static bool TryFindeMonitor(string geraet, Window fenster, out Rect arbeitsbereich)
        {
            arbeitsbereich = Rect.Empty;
            if (string.IsNullOrWhiteSpace(geraet))
            {
                return false;
            }

            foreach (IntPtr monitor in AlleMonitore())
            {
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (GetMonitorInfoEx(monitor, ref info)
                    && string.Equals(info.szDevice, geraet, StringComparison.OrdinalIgnoreCase))
                {
                    arbeitsbereich = ToDip(fenster, info.rcWork);
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<IntPtr> AlleMonitore()
        {
            var liste = new List<IntPtr>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                liste.Add(hMonitor);
                return true;
            }, IntPtr.Zero);
            return liste;
        }

        private static Rect ToDip(Window fenster, RECT rect)
        {
            PresentationSource? quelle = PresentationSource.FromVisual(fenster);
            Matrix m = quelle?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            Point obenLinks = m.Transform(new Point(rect.Left, rect.Top));
            Point untenRechts = m.Transform(new Point(rect.Right, rect.Bottom));
            return new Rect(obenLinks, untenRechts);
        }
    }
}

