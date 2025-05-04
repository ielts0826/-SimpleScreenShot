using System;
using System.Runtime.InteropServices;

namespace ScreenCaptureTool
{
    internal static class NativeMethods
    {
        // 系统指标常量
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        
        // 获取系统指标
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);
        
        // Hotkey registration
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        // Hotkey Modifiers
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;


        // Device Context (DC) functions
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        // BitBlt function for screen capture
        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        
        // Constants for GetDeviceCaps
        public const int HORZRES = 8;
        public const int VERTRES = 10;
        public const int LOGPIXELSX = 88;
        public const int LOGPIXELSY = 90;
    }
}
