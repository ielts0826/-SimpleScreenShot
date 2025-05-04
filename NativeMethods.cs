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
         // public const int HORZRES = 8; // Removed duplicate
         // public const int VERTRES = 10; // Removed duplicate
          public const int HORZRES = 8;
          public const int VERTRES = 10;
          public const int LOGPIXELSX = 88;
          public const int LOGPIXELSY = 90;

        // --- Window Creation & Management for Global Hotkeys ---

        // WindowProc delegate
        public delegate IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        // WNDCLASSEX structure
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            [MarshalAs(UnmanagedType.FunctionPtr)]
            public WindowProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName; // Can be null
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        // RegisterClassEx function
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        // UnregisterClass function
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        // GetModuleHandle function
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName); // Use nullable string for null input

        // CreateWindowEx function
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
           uint dwExStyle,
           //ushort classAtom, // Can use string class name directly
           string lpClassName, // Use string class name
           string? lpWindowName, // Nullable for no title
           uint dwStyle, // Window style (e.g., 0 for message-only)
           int x, // Typically 0 for message-only
           int y, // Typically 0 for message-only
           int nWidth, // Typically 0 for message-only
           int nHeight, // Typically 0 for message-only
           IntPtr hWndParent, // Use HWND_MESSAGE for message-only windows
           IntPtr hMenu, // Typically IntPtr.Zero
           IntPtr hInstance, // Typically GetModuleHandle(null)
           IntPtr lpParam); // Typically IntPtr.Zero

        // Special HWND value for message-only windows
        public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        // DestroyWindow function
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        // DefWindowProc function
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        // Window Messages (specifically WM_HOTKEY)
        public const uint WM_HOTKEY = 0x0312;
        // Add other messages if needed, e.g., WM_DESTROY
        public const uint WM_DESTROY = 0x0002;
     }
 }
