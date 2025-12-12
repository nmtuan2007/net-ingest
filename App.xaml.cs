using System.Windows;

namespace NetIngest;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Tự khởi tạo MainWindow tại đây để bắt lỗi
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // Nếu có lỗi, hiện MessageBox thông báo
            string errorMsg = $"Startup Error:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                errorMsg += $"\n\nInner Exception:\n{ex.InnerException.Message}";
            }
            
            MessageBox.Show(errorMsg, "CRITICAL ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}