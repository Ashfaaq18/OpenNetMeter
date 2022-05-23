using OpenNetMeter.Views;
using System.Windows;

namespace OpenNetMeter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {

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
    }
}
