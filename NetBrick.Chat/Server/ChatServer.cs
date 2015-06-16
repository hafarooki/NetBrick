using NetBrick.Chat.Server.Handlers.Requests;
using NetBrick.Core.Server;

namespace NetBrick.Chat.Server
{
    public abstract class ChatServer : BrickServer
    {
        protected ChatServer() : base(TODO, TODO, TODO, TODO)
        {
            AddHandler((new ChatRequestHandler()));
        }
    }
}