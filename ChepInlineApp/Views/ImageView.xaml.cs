using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ChepInlineApp.Enums;
using HalconDotNet;

namespace ChepInlineApp.Views
{
    /// <summary>
    /// Interaction logic for ImageView.xaml
    /// </summary>
    public partial class ImageView : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ImageView), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(CameraStatus), typeof(ImageView), new PropertyMetadata(CameraStatus.Disconnected));

        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register(nameof(Image), typeof(HImage), typeof(ImageView), new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public CameraStatus Status
        {
            get => (CameraStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public HImage? Image
        {
            get => (HImage?)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        public ImageView()
        {
            InitializeComponent();
        }
    }
}
