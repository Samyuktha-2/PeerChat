using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PeerChat.Command
{
    public class RelayCommandT<T> : ICommand
    {
        private readonly Action<T> _execute;

        public RelayCommandT(Action<T> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            if (parameter is T param)
                _execute(param);
        }

        public event EventHandler CanExecuteChanged;
    }
}
