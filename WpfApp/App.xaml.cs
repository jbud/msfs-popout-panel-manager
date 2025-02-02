﻿using MSFSPopoutPanelManager.Shared;
using MSFSPopoutPanelManager.WindowsAgent;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MSFSPopoutPanelManager.WpfApp
{
    public partial class App : Application
    {
        private static Mutex _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Override default WPF System DPI Awareness to Per Monitor Awareness
            // For this to work make sure [assembly:dpiawareness]
            DpiAwareness.Enable();

            const string appName = "MSFS PopOut Panel Manager";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                //app is already running! Exiting the application  
                Application.Current.Shutdown();
            }
            else
            {
                // Setup all unhandle exception handlers
                Dispatcher.UnhandledException += HandleDispatcherException;
                TaskScheduler.UnobservedTaskException += HandleTaskSchedulerUnobservedTaskException;
                AppDomain.CurrentDomain.UnhandledException += HandledDomainException;
            }

            base.OnStartup(e);
        }

        private void HandleTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            FileLogger.WriteException(e.Exception.Message, e.Exception);
            //ShowExceptionDialog();
        }

        private void HandleDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (e.Exception.Message != "E_INVALIDARG")      // Ignore this error
            {
                FileLogger.WriteException(e.Exception.Message, e.Exception);
                //ShowExceptionDialog();
            }
        }

        private void HandledDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
            FileLogger.WriteException(exception.Message, exception);
            //ShowExceptionDialog();
        }

        private void ShowExceptionDialog()
        {
            var messageBoxTitle = "MSFS Pop Out Panel Manager - Critical Error!";
            var messageBoxMessage = "Application has encountered a critical error and will be closed.\nPlease see the file 'error.log' for information.";
            var messageBoxButtons = MessageBoxButton.OK;

            if (MessageBox.Show(messageBoxMessage, messageBoxTitle, messageBoxButtons) == MessageBoxResult.OK)
            {
                try
                {
                    Application.Current.Shutdown();
                }
                catch { }
            }
        }
    }

    public enum PROCESS_DPI_AWARENESS
    {
        Process_DPI_Unaware = 0,
        Process_System_DPI_Aware = 1,
        Process_Per_Monitor_DPI_Aware = 2
    }

    public enum DPI_AWARENESS_CONTEXT
    {
        DPI_AWARENESS_CONTEXT_UNAWARE = 16,
        DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = 17,
        DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = 18,
        DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = 34
    }

    public static class DpiAwareness
    {
        public static void Enable()
        {
            // Windows 8.1 added support for per monitor DPI
            if (Environment.OSVersion.Version >= new Version(6, 3, 0))
            {
                // Windows 10 creators update added support for per monitor v2
                if (Environment.OSVersion.Version >= new Version(10, 0, 15063))
                {
                    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                }
                else
                {
                    SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
                }
            }
            else
            {
                SetProcessDPIAware();
            }

            var process = WindowProcessManager.GetApplicationProcess();
            PROCESS_DPI_AWARENESS outValue;
            GetProcessDpiAwareness(process.Handle, out outValue);
        }

        [DllImport("User32.dll")]
        internal static extern bool SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiFlag);

        [DllImport("SHCore.dll")]
        internal static extern bool SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

        [DllImport("User32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("SHCore.dll", SetLastError = true)]
        internal static extern void GetProcessDpiAwareness(IntPtr hprocess, out PROCESS_DPI_AWARENESS awareness);
    }
}