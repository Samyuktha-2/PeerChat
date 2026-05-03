using PeerChat.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;   
using System.Net.Sockets;
using System.Text;
using PeerChat.Services;
using PeerChat.Model;
using System.Collections.ObjectModel;
using System.Threading.Tasks;


namespace PeerChat.ViewModel
{
    public class ChatWindowVM : BaseVM
    {
        private TcpClient _client;
        private string selectedUserName;
        private readonly MainVM _main;
        private readonly string _myName;
        public string SelectedUserName
        {
            get => selectedUserName;
            set
            {
                selectedUserName = value;
                OnPropertyChanged(nameof(SelectedUserName));
            }
        }
        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();

        public ChatWindowVM(string myName, MainVM main, TcpClient client)
        {
            _main = main;
            _client = client;
            _myName = myName;

            Users.Add(new UserModel { UserName = _myName });
            StartReceiveLoop();
            _ = SendMyName();
        }
        private async Task SendMyName()
        {
            var stream = _client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(_myName);

            await MessageProtocol.SendFrameAsync(stream, 0x01, data);
        }
        private void StartReceiveLoop()
        {
            Task.Run(async () =>
            {
                var stream = _client.GetStream();

                while (true)
                {
                    var (type, payload) = await MessageProtocol.ReceiveFrameAsync(stream);

                    if (type == 0x01) // text OR name
                    {
                        string text = Encoding.UTF8.GetString(payload);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Users.Count == 1)
                            {
                                Users.Add(new UserModel { UserName = text });
                                SelectedUserName = text;
                            }
                            else
                            {
                                Messages.Add(new MessageModel
                                {
                                    Text = text,
                                    IsSentByMe = false
                                });
                            }
                        });
                    }
                }
            });
        }

    }
}
