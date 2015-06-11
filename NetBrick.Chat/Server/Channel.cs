using NetBrick.Core.Server;
using System.Collections.Generic;

namespace NetBrick.Chat.Server
{
    public class Channel
    {
        public List<string> Participants { get; }
        
        public Channel()
        {
            Participants = new List<string>();
        }

        public void AddParticipant(string participant)
        {
            Participants.Add(participant);
        }

        public void RemoveParticipant(string participant)
        {
            Participants.Remove(participant);
        }
    }
}