using BanDoSu.Core;
using System.Windows;

namespace BanDoSu.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Creates the three sibling folders before the main window loads.
            // No engine download or dependency installation happens automatically.
            new PortableStudioPaths().Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể tạo các thư mục Portable cạnh ứng dụng: " + ex.Message +
                "\nHãy giải nén ứng dụng vào thư mục có quyền ghi (ví dụ D:\\BanDoSuStudio), không dùng Program Files.",
                "Bản Đồ Sử Studio — Thiết lập thư mục",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        base.OnStartup(e);
    }
}
