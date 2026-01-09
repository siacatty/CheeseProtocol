using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol.Protocols
{
    public class NoOpProtocol : IProtocol
    {
        public string Id => "noop";
        public string DisplayName => "Unknown protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return true;
        }

        public void Execute(ProtocolContext ctx)
        {
            return;
        }
    }
}