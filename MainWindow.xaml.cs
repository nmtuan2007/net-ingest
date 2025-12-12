using System.Windows;

namespace NetIngest
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // DataContext đã được set trong XAML, hoặc set ở đây:
            // DataContext = new ViewModels.MainViewModel();
        }
    }
}
