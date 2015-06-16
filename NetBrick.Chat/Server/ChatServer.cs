using NetBrick.Chat.Server.Handlers.Requests;
using NetBrick.Core.Server;

namespace NetBrick.Chat.Server
{
    public abstract class ChatServer : BrickServer
    {
        protected ChatServer()
        {
            AddHandler((new ChatRequestHandler()));
        }
    }
}