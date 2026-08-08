using System;
using System.Windows;

namespace FoulzExternal
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // The app now boots through IMGUI.Program (single overlay for
            // menu + ESP) set as the StartupObject. Nothing else to do here.
        }
    }
}