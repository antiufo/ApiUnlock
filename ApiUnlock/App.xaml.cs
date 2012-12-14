using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ApiUnlock
{
    sealed unsafe partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }


        public delegate int MessageBoxWFunction(void* hwnd, [MarshalAs(UnmanagedType.LPWStr)] string lpText, [MarshalAs(UnmanagedType.LPWStr)] string lpCaption, uint uType);
        public delegate void* GetForegroundWindowFunction();


        private delegate int IntReturner();
        private static byte[] returnNumber = new byte[]{
            0xB8, 0x2A, 0x00, 0x00, 0x00, // MOV AEX, 42
            0xC3 // RET
        };

        protected unsafe override void OnLaunched(LaunchActivatedEventArgs args)
        {

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                if (!rootFrame.Navigate(typeof(MainPage), args.Arguments))
                    throw new Exception("Failed to create initial page");
            }
            Window.Current.Activate();


            // Example: unrestricted reflection
            ApiUnlocker.SetField(typeof(Exception), "_className", new Exception(), "example");

            // Example: dynamic code generation
            var memory = ApiUnlocker.AllocateExecutableMemory(1024);
            Marshal.Copy(returnNumber, 0, new IntPtr(memory), returnNumber.Length);
            var func = ApiUnlocker.GetDelegateForFunctionPointer<IntReturner>(memory);
            var result = func();

            // Example: Win32 APIs
            Task.Delay(1000).GetAwaiter().OnCompleted(() =>
            {
                var MessageBoxW = ApiUnlocker.GetWin32Function<MessageBoxWFunction>("user32.dll", "MessageBoxW");
                var GetForegroundWindow = ApiUnlocker.GetWin32Function<GetForegroundWindowFunction>("user32.dll", "GetForegroundWindow");
                MessageBoxW(GetForegroundWindow(), "Hello from Win32! Our dynamically produced machine code returned: " + result, "Win32", 0);
            });
        }

    }
}
