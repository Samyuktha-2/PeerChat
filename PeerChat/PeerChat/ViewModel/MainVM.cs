using PeerChat.View;
using PeerChat.ViewModel;
using System.Collections.Generic;

namespace peerchat.viewmodel
{
    public class MainVM : BaseVM
    {  
        private object currentView;
        public object CurrentView
        {
            get => currentView;
            set
            {
                currentView = value;
                OnPropertyChanged(nameof(CurrentView));
            }
        }

        public Stack<object> navigationStack = new Stack<object>();

        public MainVM()
        {
           Navigate( new ConnectionWindowVM(this));
             
        }

        public void Navigate(object vm)
        {
            navigationStack.Push(vm);
            CurrentView = vm;
        }

        public void GoBack()
        {
            if(navigationStack.Count > 1)
            {
                navigationStack.Pop();
                CurrentView = navigationStack.Peek();
            }
        }
        
    }
}
