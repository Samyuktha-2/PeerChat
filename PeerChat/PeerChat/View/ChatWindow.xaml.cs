using PeerChat.ViewModel;
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

namespace PeerChat.View
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : UserControl
    {
        public ChatWindow()
        {
            InitializeComponent();
            Loaded += ChatWindow_Loaded; 
        }

        private void ChatWindow_Loaded(object sender, RoutedEventArgs e)
        {   
            var window = Window.GetWindow(this);

            if (window != null)
            {
                window.Closing += async (s, args) =>
                {
                    if (DataContext is ChatWindowVM vm)
                    {
                        await vm.HandleLocalClosingAsync();
                    }
                };
            }
        }

        private void Image_Click(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            if (image?.Source == null) return;

            var window = new Window
            {
                Title = "Image Preview",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.Black,
                Owner = Application.Current.MainWindow,  
                Content = new Image
                {
                    Source = image.Source,
                    Stretch = Stretch.Uniform
                }
            };

            window.ShowDialog();
        } 

        private async void MessageBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ChatWindowVM vm)
                {
                    await vm.SendTypingStatus(false);
                }
            }
            catch(Exception ex)
            {

            }
        }
    }
}
