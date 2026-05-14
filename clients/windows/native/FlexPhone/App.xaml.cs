using System;
using System.Windows;
using FlexPhone.Views;

namespace FlexPhone
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;
        private static bool _ownsSingleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _singleInstanceMutex = new Mutex(true, "DevineCreations.FlexPhone.SingleInstance", out var isFirstInstance);
            _ownsSingleInstanceMutex = isFirstInstance;
            if (!isFirstInstance)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex?.ReleaseMutex();
                _ownsSingleInstanceMutex = false;
            }

            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
