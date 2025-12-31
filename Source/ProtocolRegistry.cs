using System.Collections.Generic;
using CheeseProtocol.Protocols;

namespace CheeseProtocol
{
    public static class ProtocolRegistry
    {
        private static readonly List<IProtocol> protocols = new List<IProtocol>
        {
            new ColonistProtocol(),
            new RaidProtocol(),
            new CaravanProtocol(),
            new MeteorProtocol(),
            new NoOpProtocol()
        };

        public static IReadOnlyList<IProtocol> All => protocols;

        public static IProtocol ById(string id)
        {
            foreach (var p in protocols)
                if (p.Id == id) return p;
            return null;
        }
    }
}