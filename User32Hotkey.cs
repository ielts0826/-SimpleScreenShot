using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Input; // Assuming Key and ModifierKeys are from Avalonia
using Avalonia.Threading; // For Dispatcher
using System.Reactive.Disposables; // For Disposable.Create

namespace ScreenCaptureTool
{
    // Based on the GitHub example provided by the user
    public static class User32Hotkey
    {
        // Keep delegate alive
        private static NativeMethods.WindowProc? _wndProcDelegate;
        private static IntPtr _hwnd = IntPtr.Zero; // Use IntPtr instead of HWND struct if not defined elsewhere
        private static ushort _classAtom = 0;
        private static string? _className; // Store class name for unregistering
        private static IntPtr _hInstance = IntPtr.Zero; // Store instance handle

        private static readonly Dictionary<int, Action> _registeredKeys = new();

        // Static constructor to ensure window is created only once
        static User32Hotkey()
        {
            // Run window creation on UI thread to ensure WndProc runs there?
             // Or maybe better on a dedicated thread if WndProc blocks?
             // Let's try UI thread first, assuming WndProc is lightweight.
             // Dispatcher.UIThread.Post(CreateMessageWindow, DispatcherPriority.Background); // Removed Post, run synchronously
             CreateMessageWindow(); // Run synchronously in static constructor
          }

          // Public method to register a hotkey
         public static IDisposable Create(Key key, KeyModifiers modifiers, Action execute) // Changed ModifierKeys to KeyModifiers
         {
             // Validate input
             if (key == Key.None || modifiers == KeyModifiers.None) // Changed ModifierKeys to KeyModifiers
            {
                throw new ArgumentException("Key and Modifiers cannot be None.");
            }
            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            // Ensure window handle is created (should be by static constructor, but check)
            if (_hwnd == IntPtr.Zero)
            {
                 // This might happen if Dispatcher hasn't run CreateMessageWindow yet.
                 // Consider a more robust initialization or throw an error.
                 MainWindow.LogToFile("Error: User32Hotkey message window handle is not yet created.");
                 throw new InvalidOperationException("Hotkey message window not initialized.");
            }

            // Convert Avalonia Key and Modifiers to Win32 Virtual Key Code and Modifiers
            // IMPORTANT: Need to verify Avalonia's Key -> VK mapping. Using the helper from MainWindow for now.
            uint virtualKeyCode = KeyInterop.VirtualKeyFromKey(key);
            if (virtualKeyCode == 0)
            {
                 MainWindow.LogToFile($"Error: Could not convert Avalonia Key '{key}' to Virtual Key Code.");
                 throw new ArgumentException($"Invalid key: {key}");
             }

             uint fsModifiers = 0;
             if (modifiers.HasFlag(KeyModifiers.Alt)) fsModifiers |= NativeMethods.MOD_ALT; // Changed ModifierKeys to KeyModifiers
             if (modifiers.HasFlag(KeyModifiers.Control)) fsModifiers |= NativeMethods.MOD_CONTROL; // Changed ModifierKeys to KeyModifiers
             if (modifiers.HasFlag(KeyModifiers.Shift)) fsModifiers |= NativeMethods.MOD_SHIFT; // Changed ModifierKeys to KeyModifiers
             if (modifiers.HasFlag(KeyModifiers.Meta)) fsModifiers |= NativeMethods.MOD_WIN; // Changed ModifierKeys to KeyModifiers, Assuming Meta maps to Win key

             // Generate a unique ID for the hotkey combination
            int id = (int)virtualKeyCode + ((int)fsModifiers * 0x10000); // Simple unique ID generation

            MainWindow.LogToFile($"Attempting to register hotkey: ID={id}, Modifiers={fsModifiers}, VK={virtualKeyCode}, HWND={_hwnd}");

            // Check if already registered (by this instance)
            if (_registeredKeys.ContainsKey(id))
            {
                MainWindow.LogToFile($"Error: Hotkey with ID {id} already registered by this application.");
                throw new InvalidOperationException("Hot key with this key combination is already registered by this application.");
            }

            // Register the hotkey with the hidden window
            if (!NativeMethods.RegisterHotKey(_hwnd, id, fsModifiers, virtualKeyCode))
            {
                int errorCode = Marshal.GetLastWin32Error();
                MainWindow.LogToFile($"Error: RegisterHotKey failed for ID {id}. Win32 Error Code: {errorCode}");
                throw new Win32Exception(errorCode, $"Failed to register hotkey (ID: {id}).");
            }

            MainWindow.LogToFile($"Hotkey registered successfully: ID={id}");

            // Store the action associated with this ID
            _registeredKeys.Add(id, execute);

            // Return an IDisposable to handle unregistration
            return Disposable.Create(() =>
            {
                MainWindow.LogToFile($"Unregistering hotkey: ID={id}, HWND={_hwnd}");
                if (_hwnd != IntPtr.Zero) // Check if window still exists
                {
                    if (!NativeMethods.UnregisterHotKey(_hwnd, id))
                    {
                         int errorCode = Marshal.GetLastWin32Error();
                         // Log error but don't throw in Dispose
                         MainWindow.LogToFile($"Warning: UnregisterHotKey failed for ID {id}. Win32 Error Code: {errorCode}");
                    }
                }
                _registeredKeys.Remove(id);
                MainWindow.LogToFile($"Hotkey unregistered and removed: ID={id}");
            });
        }

        // Creates the hidden message-only window
        private static void CreateMessageWindow()
        {
             MainWindow.LogToFile("User32Hotkey: Creating message window...");
             if (_hwnd != IntPtr.Zero) // Already created
             {
                  MainWindow.LogToFile("User32Hotkey: Message window already exists.");
                  return;
             }

            _hInstance = NativeMethods.GetModuleHandle(null);
            if (_hInstance == IntPtr.Zero)
            {
                 int errorCode = Marshal.GetLastWin32Error();
                 MainWindow.LogToFile($"Error: GetModuleHandle failed. Win32 Error Code: {errorCode}");
                 throw new Win32Exception(errorCode, "Failed to get module handle.");
            }

            // Ensure delegate doesn't get garbage collected
            _wndProcDelegate = new NativeMethods.WindowProc(WndProc);

            _className = "ScreenCaptureTool_HotkeyMessageWindow_" + Guid.NewGuid().ToString();

            var wndClassEx = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                hInstance = _hInstance,
                lpszClassName = _className,
                // Other fields default to 0/null
            };

            _classAtom = NativeMethods.RegisterClassEx(ref wndClassEx);
            if (_classAtom == 0)
            {
                int errorCode = Marshal.GetLastWin32Error();
                MainWindow.LogToFile($"Error: RegisterClassEx failed for '{_className}'. Win32 Error Code: {errorCode}");
                _className = null; // Reset class name on failure
                throw new Win32Exception(errorCode, "Failed to register hotkey window class.");
            }
             MainWindow.LogToFile($"User32Hotkey: Window class '{_className}' registered (Atom: {_classAtom}).");

            // Create a message-only window by specifying HWND_MESSAGE as the parent
            _hwnd = NativeMethods.CreateWindowEx(
                0, // No extended styles
                _className, // Use the registered class name
                null, // No window title
                0, // No window style flags needed for message-only
                0, 0, 0, 0, // Position and size are irrelevant
                NativeMethods.HWND_MESSAGE, // Parent is HWND_MESSAGE
                IntPtr.Zero, // No menu
                _hInstance,
                IntPtr.Zero // No extra parameters
            );

            if (_hwnd == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                MainWindow.LogToFile($"Error: CreateWindowEx failed. Win32 Error Code: {errorCode}");
                // Attempt to unregister class if window creation failed
                if (!string.IsNullOrEmpty(_className) && _hInstance != IntPtr.Zero)
                {
                     NativeMethods.UnregisterClass(_className, _hInstance);
                     MainWindow.LogToFile($"User32Hotkey: Cleaned up window class '{_className}' after CreateWindowEx failure.");
                     _className = null;
                     _classAtom = 0;
                }
                throw new Win32Exception(errorCode, "Failed to create hotkey message window.");
            }

            MainWindow.LogToFile($"User32Hotkey: Message window created successfully (HWND: {_hwnd}).");
        }

        // The window procedure that handles messages for the hidden window
        private static IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            // MainWindow.LogToFile($"WndProc received message: HWND={hWnd}, Msg={uMsg}, wParam={wParam}, lParam={lParam}"); // Very verbose logging

            if (uMsg == NativeMethods.WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                MainWindow.LogToFile($"WndProc: WM_HOTKEY received, ID={hotkeyId}");
                if (_registeredKeys.TryGetValue(hotkeyId, out var action))
                {
                    MainWindow.LogToFile($"WndProc: Found action for hotkey ID {hotkeyId}. Invoking...");
                    try
                    {
                        // IMPORTANT: Action is invoked on the thread that created the window (likely UI thread).
                        // If the action is long-running, it should be dispatched to a background thread internally.
                        action();
                        MainWindow.LogToFile($"WndProc: Action for hotkey ID {hotkeyId} invoked successfully.");
                    }
                    catch (Exception ex)
                    {
                         MainWindow.LogToFile($"Error executing hotkey action for ID {hotkeyId}: {ex}");
                         // Decide how to handle errors in the action
                    }
                    return IntPtr.Zero; // Indicate message was handled
                }
                else
                {
                     MainWindow.LogToFile($"WndProc: No action found for hotkey ID {hotkeyId}.");
                }
            }
            else if (uMsg == NativeMethods.WM_DESTROY)
            {
                 MainWindow.LogToFile($"WndProc: WM_DESTROY received for HWND={hWnd}.");
                 // Potentially handle cleanup here if needed, though DestroyWindow should trigger this.
            }

            // For all other messages, pass them to the default window procedure.
            return NativeMethods.DefWindowProc(hWnd, uMsg, wParam, lParam);
        }

        // Method to clean up resources (call on application exit)
        public static void Cleanup()
        {
            MainWindow.LogToFile("User32Hotkey: Starting cleanup...");

            // Unregister all hotkeys associated with the window (optional, Dispose should handle)
            // foreach (var id in _registeredKeys.Keys.ToList()) // ToList to avoid modification during iteration
            // {
            //     if (_hwnd != IntPtr.Zero) NativeMethods.UnregisterHotKey(_hwnd, id);
            // }
            // _registeredKeys.Clear();
            // MainWindow.LogToFile("User32Hotkey: All hotkeys unregistered (forced).");


            // Destroy the hidden window
            if (_hwnd != IntPtr.Zero)
            {
                if (NativeMethods.DestroyWindow(_hwnd))
                {
                     MainWindow.LogToFile($"User32Hotkey: Message window (HWND: {_hwnd}) destroyed successfully.");
                }
                else
                {
                     int errorCode = Marshal.GetLastWin32Error();
                     MainWindow.LogToFile($"Warning: DestroyWindow failed for HWND {_hwnd}. Win32 Error Code: {errorCode}");
                }
                _hwnd = IntPtr.Zero;
            }

            // Unregister the window class
            if (!string.IsNullOrEmpty(_className) && _hInstance != IntPtr.Zero)
            {
                if (NativeMethods.UnregisterClass(_className, _hInstance))
                {
                     MainWindow.LogToFile($"User32Hotkey: Window class '{_className}' unregistered successfully.");
                }
                else
                {
                     int errorCode = Marshal.GetLastWin32Error();
                     MainWindow.LogToFile($"Warning: UnregisterClass failed for '{_className}'. Win32 Error Code: {errorCode}");
                }
                _className = null;
                _classAtom = 0;
                _hInstance = IntPtr.Zero;
            }
             _wndProcDelegate = null; // Allow delegate to be collected if possible
            MainWindow.LogToFile("User32Hotkey: Cleanup finished.");
        }
    }
}
