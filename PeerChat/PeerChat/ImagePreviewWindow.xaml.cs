using System.Windows;
using System.Windows.Media.Imaging;

namespace PeerChat
{
    public partial class ImagePreviewWindow : Window
    {
        public ImagePreviewWindow(BitmapImage image, string title)
        {
            InitializeComponent();
            Title = title;
            PreviewImage.Source = image;
        }
    }
}
