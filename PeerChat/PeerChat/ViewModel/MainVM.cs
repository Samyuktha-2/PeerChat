using PeerChat.Command;
using PeerChat.View;
using System;
using System.Windows;
using System.Windows.Input;

namespace PeerChat.ViewModel
{
    public class MainVM : BaseVM
    {
        public ConnectionWindowVM ConnectionWindow { get; set; }
        public ICommand ThemeCommand { get; }

        public object CurrentView
        {
            get => currentView;
            set
            {
                currentView = value;
                OnPropertyChanged(nameof(CurrentView));
            }
        }
        public MainVM()
        {
            ConnectionWindow = new ConnectionWindowVM(this);
            ThemeCommand = new RelayCommand(ChangeTheme);

            CurrentView =  new ConnectionWindow();
        }

        private bool isDark = true;
        private object currentView;

        private void ChangeTheme()
        {
            var app = Application.Current;
            var dictionaries = app.Resources.MergedDictionaries;

            dictionaries[0] = new ResourceDictionary()
            {
                Source = new Uri(
                    isDark ? "Theme/LightTheme.xaml"
                           : "Theme/DarkTheme.xaml",
                    UriKind.Relative)
            };

            isDark = !isDark;
        }
    }
}
