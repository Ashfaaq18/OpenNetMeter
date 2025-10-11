using OpenNetMeter.Views;
using System.Windows;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;
using System;

namespace OpenNetMeter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //log any unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                EventLogger.Error("Unhandled exception", (Exception)ex.ExceptionObject);
            };

            EventLogger.Info("Application starting");

            bool startMinimized = false;
            for (int i = 0; i != e.Args.Length; ++i)
            {
                if (e.Args[i] == "/StartMinimized")
                    startMinimized = true;
            }
            MainWindow window = new MainWindow();
            if (startMinimized)
                window.Exit_Button_Click(null, null);
            else
                window.Show();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            EventLogger.Info("Application exiting");
            SettingsManager.Save();
        }
    }
}
