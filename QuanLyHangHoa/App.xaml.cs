using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace QuanLyHangHoa
{
    public partial class App : Application
    {
        public static Task DatabaseReady { get; private set; } = Task.CompletedTask;

        protected override async void OnStartup(StartupEventArgs e)
        {
            var startup = Stopwatch.StartNew();
            base.OnStartup(e);
            RegisterUnhandledExceptionLogging();

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DatabaseReady = ready.Task;

            var login = new LoginView();
            MainWindow = login;
            login.Show();
            Trace.WriteLine($"[STARTUP] Login shown: {startup.ElapsedMilliseconds} ms");

            var initializer = new DatabaseInitializer(
                () => new AppDbContext(),
                AppDomain.CurrentDomain.BaseDirectory);

            try
            {
                await Task.Run(initializer.Initialize);
                ready.SetResult();
                Trace.WriteLine($"[STARTUP] Database ready: {startup.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                ready.SetException(ex);
                CrashLogger.Write(ex, "Database initialization");
                Trace.WriteLine($"[STARTUP] Database failed after {startup.ElapsedMilliseconds} ms: {ex.Message}");
            }
        }

        private void RegisterUnhandledExceptionLogging()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            CrashLogger.Write(e.Exception, "WPF dispatcher");
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                CrashLogger.Write(exception, "AppDomain");
            }
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            CrashLogger.Write(e.Exception, "Unobserved task");
        }
    }
}
