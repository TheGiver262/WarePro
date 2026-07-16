using System.Security;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Views;

public partial class SqlCredentialPromptView : Window
{
    public SqlCredentialPromptView()
    {
        InitializeComponent();
        Loaded += (_, _) => UserNameInput.Focus();
    }

    public SqlCredential? Credential { get; private set; }

    // copy SecureString, khóa read-only rồi chuyển quyền sở hữu cho SqlCredential; chỉ dispose bản copy nếu chưa chuyển
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var userName = UserNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            ErrorText.Text = "Nhập tên đăng nhập SQL Server.";
            UserNameInput.Focus();
            return;
        }

        if (PasswordInput.SecurePassword.Length == 0)
        {
            ErrorText.Text = "Nhập mật khẩu SQL Server.";
            PasswordInput.Focus();
            return;
        }

        SecureString? password = null;
        try
        {
            password = PasswordInput.SecurePassword.Copy();
            password.MakeReadOnly();
            Credential = new SqlCredential(userName, password);
            password = null;
            DialogResult = true;
        }
        finally
        {
            password?.Dispose();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
