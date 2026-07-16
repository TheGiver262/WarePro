using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Startup;
using QuanLyHangHoa.Views;
using System;
using System.Diagnostics;
using System.Threading;
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
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DatabaseReady = ready.Task;
            var credentialCoordinator = FirstRunCredentialCoordinator.CreateDefault();

            Microsoft.Data.SqlClient.SqlCredential? PromptForCredential()
            {
                var prompt = new SqlCredentialPromptView();
                return prompt.ShowDialog() == true ? prompt.Credential : null;
            }

            bool TryEnsureCredential(bool replaceExisting, out bool hasCredential)
            {
                try
                {
                    hasCredential = credentialCoordinator.EnsureCredential(
                        PromptForCredential,
                        replaceExisting);
                    return true;
                }
                catch (Exception ex)
                {
                    var failure = new StartupFailureException(
                        "CFG-CREDENTIAL-STARTUP",
                        "Không thể đọc hoặc lưu tài khoản SQL trong Windows Credential Manager.",
                        SensitiveDataRedactor.Redact(ex.ToString()),
                        ex);
                    hasCredential = false;
                    ready.SetException(failure);
                    CrashLogger.Write(failure, "SQL credential startup");
                    MessageBox.Show(
                        $"{failure.UserMessage}\n\nMã lỗi: {failure.Code}",
                        "WarePro chưa thể khởi động",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Shutdown(-1);
                    return false;
                }
            }

            if (!TryEnsureCredential(replaceExisting: false, out var hasCredential))
            {
                return;
            }
            if (!hasCredential)
            {
                ready.SetCanceled();
                Shutdown(0);
                return;
            }

            var coordinator = StartupCoordinator.CreateDefault();
            var result = await coordinator.RunAsync(CancellationToken.None);
            if (!result.Success && result.ErrorCode == "SQL-CREDENTIAL-REJECTED")
            {
                if (!TryEnsureCredential(replaceExisting: true, out hasCredential))
                {
                    return;
                }
                if (!hasCredential)
                {
                    ready.SetCanceled();
                    Shutdown(0);
                    return;
                }

                result = await coordinator.RunAsync(CancellationToken.None);
            }

            if (!result.Success)
            {
                var failure = new StartupFailureException(
                    result.ErrorCode ?? "INST-STARTUP-FAILED",
                    result.UserMessage,
                    result.TechnicalDetailRedacted);
                ready.SetException(failure);
                CrashLogger.Write(failure, "Application startup");
                Trace.WriteLine($"[STARTUP] Failed after {startup.ElapsedMilliseconds} ms: {result.ErrorCode}");
                MessageBox.Show(
                    $"{result.UserMessage}\n\nMã lỗi: {result.ErrorCode}\nLog: {result.LogPath}",
                    "WarePro chưa thể khởi động",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            ready.SetResult();
            Trace.WriteLine($"[STARTUP] Database ready: {startup.ElapsedMilliseconds} ms");

            var login = new LoginView();
            MainWindow = login;
            login.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            Trace.WriteLine($"[STARTUP] Login shown: {startup.ElapsedMilliseconds} ms");
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
