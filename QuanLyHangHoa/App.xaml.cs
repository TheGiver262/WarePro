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
    /// <summary>
    /// quản lý vòng đời ứng dụng và chỉ mở màn hình đăng nhập sau khi cơ sở dữ liệu sẵn sàng.
    /// </summary>
    public partial class App : Application
    {
        private StartupCoordinator? _startupCoordinator;
        // các màn hình khởi tạo sau có thể chờ task này thay vì tự kiểm tra lại cơ sở dữ liệu.
        public static Task DatabaseReady { get; private set; } = Task.CompletedTask;

        protected override async void OnStartup(StartupEventArgs e)
        {
            var startup = Stopwatch.StartNew();
            base.OnStartup(e);
            RegisterUnhandledExceptionLogging();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // continuations chạy tách khỏi callback startup để không nối thêm việc nặng vào UI thread.
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DatabaseReady = ready.Task;
            var credentialCoordinator = FirstRunCredentialCoordinator.CreateDefault();

            // credential chỉ tồn tại qua lời gọi này; coordinator chịu trách nhiệm lưu bằng kho bảo mật Windows.
            Microsoft.Data.SqlClient.SqlCredential? PromptForCredential()
            {
                var prompt = new SqlCredentialPromptView();
                return prompt.ShowDialog() == true ? prompt.Credential : null;
            }

            // trả riêng trạng thái thao tác và trạng thái có credential để phân biệt lỗi với việc người dùng hủy.
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

            // luôn chuẩn bị credential trước khi probe vì mọi bước startup phía sau đều dùng cùng kết nối.
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

            _startupCoordinator = StartupCoordinator.CreateDefault();
            var coordinator = _startupCoordinator;
            // chờ coordinator kiểm tra database sẵn sàng và đăng ký lease client trước khi mở UI.
            var result = await coordinator.RunAsync(CancellationToken.None);
            // chỉ hỏi thay credential khi SQL Server xác nhận tài khoản bị từ chối; các lỗi khác giữ nguyên nguyên nhân.
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

            // phát tín hiệu sẵn sàng trước khi mở login để mọi màn hình sau thấy cùng một trạng thái.
            ready.SetResult();
            Trace.WriteLine($"[STARTUP] Database ready: {startup.ElapsedMilliseconds} ms");

            var login = new LoginView();
            MainWindow = login;
            login.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            Trace.WriteLine($"[STARTUP] Login shown: {startup.ElapsedMilliseconds} ms");
        }

        // ba kênh này bao phủ lỗi trên UI thread, thread ngoài WPF và task không được await.
        // handler chỉ ghi log; chính luồng phát sinh vẫn quyết định ứng dụng có dừng hay không.
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _startupCoordinator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                base.OnExit(e);
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
