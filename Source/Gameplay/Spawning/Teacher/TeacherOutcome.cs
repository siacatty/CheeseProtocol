using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class TeacherOutcome: IExposable
    {
        private string colorPositive = "#2e8032ff";
        private string colorNegative = "#aa4040ff";
        private HashSet<string> studentUIDs;
        private List<string> studentUIDsList = null;
        private Dictionary<string, string> escapedStudentUIDs;
        private int skillUp;
        private float passionUpProb;

        public TeacherOutcome()
        {
            escapedStudentUIDs = new Dictionary<string, string>();
        }
        public TeacherOutcome(int skillUp, float passionUpProb)
        {
            this.skillUp = skillUp;
            this.passionUpProb = passionUpProb;
            escapedStudentUIDs = new();
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                studentUIDsList = studentUIDs != null ? new List<string>(studentUIDs) : null;

            Scribe_Values.Look(ref skillUp, "skillUp", 0);
            Scribe_Values.Look(ref passionUpProb, "passionUpProb", 0f);

            Scribe_Collections.Look(ref studentUIDsList, "studentUIDs", LookMode.Value);
            Scribe_Collections.Look(ref escapedStudentUIDs, "escapedStudentUIDs", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                studentUIDs = studentUIDsList != null ? new HashSet<string>(studentUIDsList) : null;
                if (escapedStudentUIDs == null)
                    escapedStudentUIDs = new Dictionary<string, string>();
            }
        }

        public void SetStudents(HashSet<string> studentUIDs)
        {
            this.studentUIDs = studentUIDs;
        }

        public void AddEscapeStudent(Pawn pawn)
        {
            var uid = pawn?.GetUniqueLoadID();
            if (string.IsNullOrEmpty(uid)) return; 
            escapedStudentUIDs[uid] = pawn.NameShortColored;
        }

        public void Apply(Pawn teacher, List<Pawn> pawns)
        {
            var sb = new StringBuilder();
            var remainingStudentUIDs = new HashSet<string>(studentUIDs);
            sb.AppendLine("수업 완료 :");
            for (int i=0; i< pawns.Count ; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p == teacher) continue;
                remainingStudentUIDs.Remove(p.GetUniqueLoadID());
                string pawnSummary = $"    {p.NameShortColored} : ";
                pawnSummary += ApplySkill(p);
                ApplyBoringThought(p);
                //pawnSummary += ApplyPassion(p);
                sb.AppendLine(pawnSummary);
            }
            sb.AppendLine();
            sb.AppendLine("도주 성공 :");
            foreach (var kv in escapedStudentUIDs)
            {
                var uid = kv.Key;
                var name = kv.Value;
                remainingStudentUIDs.Remove(uid);
                sb.AppendLine($"    {name}");
            }
            if (remainingStudentUIDs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("수업 실패 :");
                foreach (var remainingUid in remainingStudentUIDs)
                {
                    var p = TryResolvePawn(teacher, remainingUid);
                    if (p == null) continue;
                    sb.AppendLine($"    {p.NameShortColored}");
                }
            }
            CheeseLetter.SendCheeseLetter(CheeseCommand.Teacher, "수업 끝", sb.ToString(), teacher, null, teacher.Map, LetterDefOf.PositiveEvent);
        }

        private string ApplySkill(Pawn pawn)
        {
            if (pawn?.skills == null) return string.Empty;

            int remaining = skillUp;
            if (remaining <= 0) return string.Empty;
            List<SkillRecord> list = new List<SkillRecord>(pawn.skills.skills);
            list.Shuffle();
            list.Sort((a, b) => b.Level.CompareTo(a.Level));
            Dictionary<SkillDef, int> applied = new Dictionary<SkillDef, int>();
            for (int i = 0; i < list.Count && remaining > 0; i++)
            {
                SkillRecord sr = list[i];
                if (sr == null) continue;

                int cap = GameplayConstants.SkillLevelMax;
                if (sr.Level >= cap) continue;

                int give = Mathf.Min(3, remaining);
                int room = cap - sr.Level;
                if (room <= 0) continue;

                int inc = Mathf.Min(give, room);
                if (inc <= 0) continue;
                for (int k = 0; k < inc; k++)
                {
                    float need = sr.XpRequiredForLevelUp - sr.xpSinceLastLevel;
                    if (need < 1f) need = 1f;
                    sr.Learn(need +1f, direct: true);
                }

                remaining -= inc;

                if (applied.TryGetValue(sr.def, out var prev))
                    applied[sr.def] = prev + inc;
                else
                    applied.Add(sr.def, inc);
            }

            if (applied.Count == 0) return string.Empty;

            // 3) format string: " (Mining: 3, Medicine: 1, Shooting: 2)"
            var sb = new StringBuilder();
            sb.Append(" (");

            int written = 0;
            foreach (var kv in applied)
            {
                if (written > 0) sb.Append(", ");

                // Use RimWorld label for readability
                string label = kv.Key?.label ?? kv.Key?.defName ?? "스킬";
                sb.Append(label);
                sb.Append(": +");
                sb.Append(kv.Value);

                written++;
            }

            sb.Append(")");
            return sb.ToString();
        }

        private void ApplyBoringThought(Pawn pawn)
        {
            if (pawn == null) return;
            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail("CheeseProtocol_BoringLesson");
            if (def == null) return;
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(def);
        }

        private string ApplyPassion(Pawn pawn)
        {
            string failPassion = $"<color={colorNegative}> (열정 부여: 실패)</color>";
            if (pawn?.skills == null) 
                return failPassion;
            if (!Rand.Chance(passionUpProb))
                return failPassion;
            List<SkillRecord> list = new List<SkillRecord>(pawn.skills.skills);
            list.Shuffle();
            list.Sort((a, b) => b.Level.CompareTo(a.Level));
            foreach (var sr in list)
            {
                if (sr == null) continue;

                if (sr.passion == Passion.Major)
                    continue;

                sr.passion = sr.passion == Passion.None
                    ? Passion.Minor
                    : Passion.Major;

                string label = sr.def?.label ?? sr.def?.defName ?? "스킬";
                return $"<color={colorPositive}> (열정 부여: {label})</color>";
            }
            return failPassion;
        }

        private static Pawn TryResolvePawn(Pawn teacher, string pawnUID)
        {
            if (teacher == null || teacher.DestroyedOrNull() || teacher.Dead) return null;
            if (pawnUID.NullOrEmpty()) return null;
            var carriedPawn = teacher.carryTracker?.CarriedThing as Pawn;
            if (carriedPawn != null && !carriedPawn.DestroyedOrNull() && !carriedPawn.Dead)
            {
                string cuid = carriedPawn.GetUniqueLoadID();
                if (!cuid.NullOrEmpty() && cuid == pawnUID)
                    return carriedPawn;
            }
            Map map = teacher.Map;
            if (map == null) return null;

            var colonists = map.mapPawns?.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0) return null;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p == null || p.DestroyedOrNull() || p.Dead) continue;

                if (p.GetUniqueLoadID() == pawnUID)
                    return p;
            }

            return null;
        }

    }
}