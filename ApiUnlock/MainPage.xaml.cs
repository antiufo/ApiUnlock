using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace ApiUnlock
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public unsafe sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            lblResult.Text = string.Empty;
        }

        public delegate int MessageBoxWFunction(void* hwnd, [MarshalAs(UnmanagedType.LPWStr)] string lpText, [MarshalAs(UnmanagedType.LPWStr)] string lpCaption, uint uType);
        public delegate void* GetForegroundWindowFunction();

        private delegate int IntReturner();


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var memory = ApiUnlocker.AllocateExecutableMemory(1024);
            var returnNumber = new byte[]{
                0xB8, 0x2A, 0x00, 0x00, 0x00, // MOV AEX, 42
                0xC3 // RET
            };
            Marshal.Copy(returnNumber, 0, new IntPtr(memory), returnNumber.Length);
            var func = ApiUnlocker.GetDelegateForFunctionPointer<IntReturner>(memory);
            var result = func();
            lblResult.Text = @"Generated executable code:
  MOV EAX, 42
  RET

Result of invocation: " + result;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var MessageBoxW = ApiUnlocker.GetWin32Function<MessageBoxWFunction>("user32.dll", "MessageBoxW");
            var GetForegroundWindow = ApiUnlocker.GetWin32Function<GetForegroundWindowFunction>("user32.dll", "GetForegroundWindow");
            lblResult.Text = "Calling MessageBoxW...";
            var result = MessageBoxW(GetForegroundWindow(), "Hello from Win32!", "Win32", 0);
            lblResult.Text = "Return value of MessageBoxW: " + result;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            lblResult.Text = "Value of Environment.s_CheckedOSType: " + ApiUnlocker.GetField(typeof(Environment), "s_CheckedOSType", null);
        }

    }
}
