using System;
using System.Collections.Generic;

namespace _Scripts.Controller
{
    public static class MessageDispatcher
    {
        public enum EMessageID
        {
            OutOfEnergy,
            FireOff
        }
        
        private static Dictionary<EMessageID, List<Action>> _messageHandlers = new ();

        public static void Register(EMessageID id, Action handler)
        {
            if (!_messageHandlers.ContainsKey(id))
            {
                _messageHandlers.Add(id, new List<Action>());
            }
            
            _messageHandlers[id].Add(handler);
        }

        public static void Unregister(EMessageID id, Action handler)
        {
            if (!_messageHandlers.ContainsKey(id))
            {
                return;
            }
            
            _messageHandlers[id].Remove(handler);
        }

        public static void Send(EMessageID id)
        {
            if (!_messageHandlers.ContainsKey(id))
            {
                return;
            }
            
            _messageHandlers[id].ForEach(action => action?.Invoke());
        }
    }
}