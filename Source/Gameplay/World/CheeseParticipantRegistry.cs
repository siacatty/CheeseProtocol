using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static CheeseProtocol.CheeseLog;

namespace CheeseProtocol
{
    /// <summary>
    /// Save-game scoped registry (stored in the .rws save file via WorldComponent).
    /// Tracks participants by chat user key -> spawned pawn thingIDNumber.
    /// </summary>
    public class CheeseParticipantRegistry : WorldComponent
    {
        private List<ParticipantRecord> ActiveRecords = new List<ParticipantRecord>();
        private List<ParticipantRecord> InActiveRecords = new List<ParticipantRecord>();
        private int lastColonistCount = -1;

        public CheeseParticipantRegistry(World world) : base(world)
        {
        }

        public static CheeseParticipantRegistry Get()
        {
            // WorldComponent is save-scoped; Find.World is the current save's world.
            return Find.World?.GetComponent<CheeseParticipantRegistry>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            //Log.Warning($"[ExposeData] mode={Scribe.mode}");
            Scribe_Collections.Look(ref ActiveRecords, "ActiveRecords", LookMode.Deep);
            Scribe_Collections.Look(ref InActiveRecords, "InActiveRecords", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ActiveRecords ??= new List<ParticipantRecord>();
                InActiveRecords ??= new List<ParticipantRecord>();

                RebindNullPawnsByUid(ActiveRecords);
                RebindNullPawnsByUid(InActiveRecords);

                PruneActive();
                PruneInActive();
            }
        }
        private static void RebindNullPawnsByUid(List<ParticipantRecord> list)
        {
            if (list == null || list.Count == 0) return;

            // 후보 풀 (비용 낮게: 한번만 가져와서)
            var pool = PawnsFinder.All_AliveOrDead; // includes dead if present in save

            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null || r.pawn != null) continue;
                if (string.IsNullOrEmpty(r.pawnUid)) continue;

                Pawn found = null;
                for (int j = 0; j < pool.Count; j++)
                {
                    var p = pool[j];
                    if (p != null && p.GetUniqueLoadID() == r.pawnUid)
                    {
                        found = p;
                        break;
                    }
                }

                if (found != null)
                    r.pawn = found;
            }
        }
        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;

            int now = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Count(p => p != null && !p.Dead);
            if (now != lastColonistCount)
            {
                QWarn($"colonist number update. {lastColonistCount} -> {now}", Channel.Debug);
                RefreshInActive();
                RefreshActive();
                lastColonistCount = now;
            }
        }

        public int Count => ActiveRecords?.Count ?? 0;

        public bool IsRegistered(string username)
        {
            if (string.IsNullOrEmpty(username) || ActiveRecords == null) return false;
            return ActiveRecords.Any(r => r != null && r.username == username);
        }

        public bool TryGetRecords(string username, out List<ParticipantRecord> result)
        {
            result = null;

            if (string.IsNullOrEmpty(username) || ActiveRecords == null || ActiveRecords.Count == 0)
                return false;

            var list = new List<ParticipantRecord>();
            for (int i = 0; i < ActiveRecords.Count; i++)
            {
                var r = ActiveRecords[i];
                if (r != null && r.username == username)
                    list.Add(r);
            }

            if (list.Count == 0)
                return false;

            result = list;
            return true;
        }

        /// <summary>
        /// Register a new participant. Returns false if already registered or capacity is full.
        /// If capacity is enabled (maxParticipants > 0), uses current record count.
        /// </summary>
        public bool TryRegister(string username, Pawn pawn, out string reason)
        {
            reason = null;
            var join = CheeseProtocolMod.Settings?.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join);

            bool restrictParticipants = join?.restrictParticipants ?? CheeseDefaults.RestrictParticipants;
            int  maxParticipants      = join?.maxParticipants      ?? CheeseDefaults.MaxParticipants;

            if (pawn == null) { reason = "invalid_pawn"; return false; }

            if (ActiveRecords == null) ActiveRecords = new List<ParticipantRecord>();

            // Avoid duplicates
            //if (IsRegistered(username)) { reason = "already_registered"; return false; }

            // Capacity check (save-scoped)
            if (restrictParticipants && ActiveRecords.Count >= maxParticipants)
            {
                QWarn($"Max participants reached. Ignoring command. current record count = {ActiveRecords.Count}");
                reason = "capacity_full";
                return false;
            }

            ActiveRecords.Add(new ParticipantRecord(username, pawn, GenTicks.TicksGame));
            return true;
        }

        public ParticipantPawnStatus GetPawnStatus(Pawn pawn)
        {
            if (pawn == null)
            {
                return ParticipantPawnStatus.Removed;
            }
            //QMsg($"GetPawnStatus: pawn.dead={pawn.Dead}, pawn.IsKidnapped(){pawn.IsKidnapped()}, pawn.Destroyed={pawn.Destroyed}. pawn.IsCaravanMember()={pawn.IsCaravanMember()} pawn.Map is null?={pawn.Map==null}", Channel.Debug);
            //QMsg($"GetPawnStatus: pawn.Corpse is null={pawn.Corpse==null}, pawn.Corpse.Spawned?{pawn.Corpse?.Spawned}", Channel.Debug);

            Map map = pawn.Map;

            if ((pawn.Destroyed && !pawn.Dead) || (pawn.Dead && pawn.Corpse.DestroyedOrNull()))
            {
                return ParticipantPawnStatus.Removed;
            }

            if (pawn.Dead || pawn.IsKidnapped())
            {
                return ParticipantPawnStatus.Inactive;
            }
            if (map != null)
            {
                return ParticipantPawnStatus.OkOnMap;
            }
            if (pawn.IsCaravanMember())
            {
                return ParticipantPawnStatus.Caravan;
            }
            return ParticipantPawnStatus.NoBubble;
        }
        public bool TryRemovePawn(Pawn pawn)
        {
            if (pawn == null) return false;
            if (ActiveRecords == null || ActiveRecords.Count == 0) return false;
            for (int i = ActiveRecords.Count - 1; i >= 0; i--)
            {
                var r = ActiveRecords[i];
                if (r == null) { ActiveRecords.RemoveAt(i); continue;}

                if (r.pawn == pawn)
                {
                    // move
                    ActiveRecords.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void PruneInActive()
        {
            if (InActiveRecords == null || InActiveRecords.Count == 0) return;

            int removed = InActiveRecords.RemoveAll(r =>
                r == null ||
                r.pawn == null
            );
            //Log.Warning($"InActive records removed {removed} items");
        }

        public void PruneActive()
        {
            if (ActiveRecords == null || ActiveRecords.Count == 0) return;

            int removed = ActiveRecords.RemoveAll(r =>
                r == null ||
                r.pawn == null
            );
            //Log.Warning($"Active records removed {removed} items");
        }


        public void RefreshActive()
        {
            if (ActiveRecords == null || ActiveRecords.Count == 0) return;

            InActiveRecords ??= new List<ParticipantRecord>();

            for (int i = ActiveRecords.Count - 1; i >= 0; i--)
            {
                var r = ActiveRecords[i];
                var p = r?.pawn;
                string name = p.Name?.ToStringShort ?? "(no-name)";
                string map = p.Map != null ? p.Map.ToString() : "null";

                // QWarn(
                //     $"Active: name={name} " +
                //     $"dead={p.Dead} " +
                //     $"destroyed={p.Destroyed} " +
                //     $"kidnapped={p.IsKidnapped()} " +
                //     $"caravan={p.IsCaravanMember()} " +
                //     $"spawned={p.Spawned} " +
                //     $"corpseNull={p.Corpse == null} " +
                //     $"corpseDestroyed={p.Corpse?.Destroyed} " +
                //     $"corpseSpawned={p.Corpse?.Spawned} " +
                //     $"corpseParentHolder={p.Corpse?.ParentHolder != null} "

                // );
                ParticipantPawnStatus status = GetPawnStatus(p);
                switch (status)
                {
                    case ParticipantPawnStatus.Removed:
                        QMsg($"RefreshActive: removing {p?.Name?.ToStringShort ?? "null"}", Channel.Debug);
                        ActiveRecords.RemoveAt(i);
                        continue;
                    case ParticipantPawnStatus.Inactive:
                        QMsg($"RefreshActive: moving {p?.Name?.ToStringShort ?? "{null}"} to InActive", Channel.Debug);
                        ActiveRecords.RemoveAt(i);
                        InActiveRecords.Add(r);
                        continue;
                    case ParticipantPawnStatus.OkOnMap:
                        continue;
                    case ParticipantPawnStatus.Caravan:
                        continue;
                    default:
                        break;
                }
            }
        }

        public void RefreshInActive()
        {
            if (InActiveRecords == null || InActiveRecords.Count == 0) return;

            ActiveRecords ??= new List<ParticipantRecord>();

            for (int i = InActiveRecords.Count - 1; i >= 0; i--)
            {
                var r = InActiveRecords[i];
                var p = r?.pawn;
                string name = p.Name?.ToStringShort ?? "(no-name)";
                string map = p.Map != null ? p.Map.ToString() : "null";
                // QWarn(
                //     $"InActive: name={name} " +
                //     $"dead={p.Dead} " +
                //     $"destroyed={p.Destroyed} " +
                //     $"kidnapped={p.IsKidnapped()} " +
                //     $"caravan={p.IsCaravanMember()} " +
                //     $"spawned={p.Spawned} " +
                //     $"corpseNull={p.Corpse == null} " +
                //     $"corpseDestroyed={p.Corpse?.Destroyed} " +
                //     $"corpseSpawned={p.Corpse?.Spawned} " +
                //     $"corpseParentHolder={p.Corpse?.ParentHolder != null} "

                // );
                if (p == null || (p.Dead && p.Corpse.DestroyedOrNull()) || (p.Destroyed && !p.Dead))
                {
                    InActiveRecords.RemoveAt(i);
                    QMsg($"RefreshInActive: removing {p?.Name?.ToStringShort ?? "null"}", Channel.Debug);
                    continue;
                }

                if (p.Map != null && p.Faction == Faction.OfPlayer && p.IsFreeColonist && !p.Destroyed && !p.Dead)
                {
                    QMsg($"RefreshInActive: moving {p?.Name?.ToStringShort ?? "null"} to Active", Channel.Debug);
                    InActiveRecords.RemoveAt(i);
                    ActiveRecords.Add(r);
                }
            }
        }

        public bool TryMoveToActive(Pawn pawn)
        {
            if (pawn == null) return false;
            if (InActiveRecords == null || InActiveRecords.Count == 0) return false;

            ActiveRecords ??= new List<ParticipantRecord>();
            for (int i = InActiveRecords.Count - 1; i >= 0; i--)
            {
                var r = InActiveRecords[i];
                if (r == null) { InActiveRecords.RemoveAt(i); continue; }

                if (r.pawn == pawn)
                {
                    // move
                    InActiveRecords.RemoveAt(i);
                    ActiveRecords.Add(r);
                    return true;
                }
            }
            return false;
        }

        public bool TryMoveToInactive(Pawn pawn)
        {
            if (pawn == null) return false;
            if (ActiveRecords == null || ActiveRecords.Count == 0) return false;

            InActiveRecords ??= new List<ParticipantRecord>();
            for (int i = ActiveRecords.Count - 1; i >= 0; i--)
            {
                var r = ActiveRecords[i];
                if (r == null) { ActiveRecords.RemoveAt(i); continue; }

                if (r.pawn == pawn)
                {
                    // move
                    ActiveRecords.RemoveAt(i);
                    InActiveRecords.Add(r);
                    return true;
                }
            }
            return false;
        }
        public bool TryUnregister(string username)
        {
            if (string.IsNullOrEmpty(username) || ActiveRecords == null) return false;
            int idx = ActiveRecords.FindIndex(r => r != null && r.username == username);
            if (idx < 0) return false;
            ActiveRecords.RemoveAt(idx);
            return true;
        }

        public string CapacityLabel()
        {
            //bool restrictParticipants = false;
            int maxParticipants = CheeseProtocolMod.Settings.GetAdvSetting<JoinAdvancedSettings>(CheeseCommand.Join).maxParticipants;
            int cur = ActiveRecords?.Count ?? 0;
            return maxParticipants > 0 ? $"{cur}/{maxParticipants}" : $"{cur}/∞";
        }

        public IEnumerable<ParticipantRecord> RecordsSafe()
        {
            if (ActiveRecords == null) yield break;
            for (int i = 0; i < ActiveRecords.Count; i++)
            {
                var r = ActiveRecords[i];
                if (r != null) yield return r;
            }
        }
    }
}