using Verse;
using RimWorld;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol.Protocols
{
    public class TeacherProtocol : IProtocol
    {
        public string Id => "teacher";
        public string DisplayName => "Teacher protocol";

        public bool CanExecute(ProtocolContext ctx)
        {
            return ctx?.Map != null;
        }

        public void Execute(ProtocolContext ctx)
        {
            var evt = ctx.CheeseEvt;
            QMsg($"Executing protocol={Id} for {evt}", Channel.Debug);
            TeacherSpawner.Spawn(evt.username, evt.amount, evt.message);
        }
    }
}