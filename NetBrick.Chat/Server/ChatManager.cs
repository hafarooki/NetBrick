using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetBrick.Chat.Server
{
    public class ChatManager
    {
        public List<Channel> Channels { get; }

        public ChatManager()
        {
            Channels = new List<Channel>();
        }

        public void AddChannel(Channel channel)
        {
            Channels.Add(channel);
        }

        public void RemoveChannel(Channel channel)
        {
            Channels.Remove(channel);
        }
    }
}
