using LibKaseya;
using Nucs.JsonSettings;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace KLC_Ex {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static KLCShared Shared;

        public App() : base() {
            if (!Debugger.IsAttached) {
                //Setup exception handling rather than closing rudely.
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
                TaskScheduler.UnobservedTaskException += (sender, args) => ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
                };
            }

            string pathShared = Path.GetDirectoryName(Environment.ProcessPath) + @"\KLC-Shared.json";
            if (File.Exists(pathShared))
                Shared = JsonSettings.Load<KLCShared>(pathShared);
            else
                Shared = JsonSettings.Construct<KLCShared>(pathShared);
        }

        void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //, Debugger.IsAttached
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            foreach (string vsa in App.Shared.VSA)
            {
                Kaseya.Start(vsa, KaseyaAuth.GetStoredAuth(vsa));
            }

            new MainWindow().Show();
        }
    }
}
