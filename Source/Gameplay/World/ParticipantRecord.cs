using System;
using System.Security.Policy;
using Verse;

namespace CheeseProtocol
{
    public class ParticipantRecord : IExposable
    {
        public string username;     // chat nick / unique id
        public Pawn pawn;
        public int joinedTick;
        public string pawnUid;

        public ParticipantRecord() { }

        public ParticipantRecord(string username, Pawn pawn, int joinedTick)
        {
            this.username = username;
            this.pawn = pawn;
            this.joinedTick = joinedTick;
            this.pawnUid = pawn?.GetUniqueLoadID();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref username, "username");
            Scribe_Values.Look(ref joinedTick, "joinedTick");
            Scribe_Values.Look(ref pawnUid, "pawnUid");
            Scribe_References.Look(ref pawn, "pawn");
            if (Scribe.mode == LoadSaveMode.Saving && pawn != null)
                pawnUid = pawn.GetUniqueLoadID();
        }

        public override string ToString() => $"{username} -> pawnId={pawn.ThingID} @tick={joinedTick}";
        public Pawn GetPawn() => pawn;
    }
}