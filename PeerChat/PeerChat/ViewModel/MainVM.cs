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
        public ChatWindowVM ChatWindow { get; set; } 

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

            CurrentView =  new ConnectionWindow();
        } 
        private object currentView;

        
    }
}
