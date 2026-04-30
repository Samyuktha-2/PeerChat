namespace PeerChat.ViewModel
{
    public class MainVM
    {
        public ConnectionWindowVM ConnectionWindow { get; set; }

        public MainVM()
        {
            ConnectionWindow = new ConnectionWindowVM();
        }
    }
}
