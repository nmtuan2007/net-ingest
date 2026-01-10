using System.IO;
using System.Windows;
using NetIngest.ViewModels;

namespace NetIngest
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string path = files[0];
                    string targetDirectory = path;

                    // If it is a file, get its directory
                    if (File.Exists(path))
                    {
                        targetDirectory = Path.GetDirectoryName(path) ?? path;
                    }

                    // Update ViewModel if valid
                    if (Directory.Exists(targetDirectory))
                    {
                        if (DataContext is MainViewModel vm)
                        {
                            vm.SourcePath = targetDirectory;
                            vm.StatusMsg = "Directory selected via drop.";
                        }
                    }
                }
            }
        }
    }
}
