using System.Windows;

namespace ChepInlineApp.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            for (double i = 0.0; i <= 1.0; i += 0.05)
            {
                this.Opacity = i;
                await Task.Delay(10);
            }

            this.Opacity = 1.0;
        }
    }
}