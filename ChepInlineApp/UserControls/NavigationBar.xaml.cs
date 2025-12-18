using ChepInlineApp.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChepInlineApp.UserControls
{
    /// <summary>
    /// Interaction logic for NavigationBar.xaml
    /// </summary>
    public partial class NavigationBar : UserControl
    {
        private NavigationBarViewModel? _viewModel;

        public NavigationBar()
        {
            InitializeComponent();
            this.DataContextChanged += NavigationBar_DataContextChanged;
        }

        private void NavigationBar_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = e.NewValue as NavigationBarViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                // Set initial width based on collapsed state
                AnimateWidth(_viewModel.IsCollapsed);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NavigationBarViewModel.IsCollapsed) && _viewModel != null)
            {
                AnimateWidth(_viewModel.IsCollapsed);
            }
        }

        private void AnimateWidth(bool isCollapsed)
        {
            double targetWidth = isCollapsed ? 60 : 250;
            double currentWidth = double.IsNaN(this.Width) || this.Width == 0 ? 60 : this.Width;

            // Clear any existing animation first
            this.BeginAnimation(UserControl.WidthProperty, null);

            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                From = currentWidth,
                To = targetWidth,
                FillBehavior = FillBehavior.HoldEnd
            };

            this.BeginAnimation(UserControl.WidthProperty, widthAnimation);
        }

        private void CollapseExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is NavigationBarViewModel viewModel)
            {
                viewModel.ToggleNavigationBarCommand.Execute(null);
                // Animation will be handled by PropertyChanged event
            }
        }
    }
}
