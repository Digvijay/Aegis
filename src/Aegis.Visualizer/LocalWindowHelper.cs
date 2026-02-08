
using System;
using System.Runtime.InteropServices;
using WinRT;

namespace Aegis.Visualizer
{
    public static class LocalWindowHelper
    {
        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        public static void InitializeWithWindow(this object target, IntPtr hwnd)
        {
            var initializeWithWindow = target.As<IInitializeWithWindow>();
            initializeWithWindow.Initialize(hwnd);
        }
        
        // Helper to get HWND from a generic Window object in WinUI 3 is tricky without more boilerplate.
        // For simplicity in this scaffold, we will assume the user has the HWND.
    }
}
