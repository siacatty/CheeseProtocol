using Verse;

namespace CheeseProtocol
{
    public class ProtocolContext
    {
        public CheeseEvent CheeseEvt { get; }
        public Map Map { get; }

        public ProtocolContext(CheeseEvent evt, Map map)
        {
            CheeseEvt = evt;
            Map = map;
        }
    }
}