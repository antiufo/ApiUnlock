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
        }

    }
}
