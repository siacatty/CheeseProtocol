using System;
using System.Collections.Generic;
using System.Linq;
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

            float remaining = (float)skillUp; // skillUp is now XP (int)
            if (remaining <= 0f) return string.Empty;

            int cap = GameplayConstants.SkillLevelMax; // usually 20

            // 1) Collect + shuffle for tie-breaking, then sort by (Level desc, progress desc)
            List<SkillRecord> all = new List<SkillRecord>(pawn.skills.skills);
            all.Shuffle();

            all.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;

                int lvl = b.Level.CompareTo(a.Level);
                if (lvl != 0) return lvl;

                float ap = SkillAsFloat(a);
                float bp = SkillAsFloat(b);
                return bp.CompareTo(ap);
            });

            // 2) Eligible list (not capped)
            List<SkillRecord> eligible = all.Where(sr => sr != null && sr.Level < cap).ToList();
            if (eligible.Count == 0) return string.Empty;

            // 3) Pick randomly among top window (0..2)
            int windowCount = Math.Min(3, eligible.Count);
            SkillRecord picked = eligible[Rand.Range(0, windowCount)];
            int idx = eligible.IndexOf(picked);
            if (idx < 0) idx = 0;

            // 4) Track before/after for touched skills
            //    store per SkillDef: (before, after)
            Dictionary<SkillDef, (float before, float after)> changed = new Dictionary<SkillDef, (float, float)>();

            // Helper: mark before if first time
            void MarkBefore(SkillRecord sr)
            {
                if (sr?.def == null) return;
                if (!changed.ContainsKey(sr.def))
                {
                    float bf = SkillAsFloat(sr);
                    changed[sr.def] = (bf, bf);
                }
            }

            void MarkAfter(SkillRecord sr)
            {
                if (sr?.def == null) return;
                if (changed.TryGetValue(sr.def, out var v))
                {
                    changed[sr.def] = (v.before, SkillAsFloat(sr));
                }
                else
                {
                    float now = SkillAsFloat(sr);
                    changed[sr.def] = (now, now);
                }
            }

            // 5) Distribute XP: dump to picked, overflow goes to next highest
            for (int i = idx; i < eligible.Count && remaining > 0.0001f; i++)
            {
                SkillRecord sr = eligible[i];
                if (sr == null) continue;
                if (sr.Level >= cap) continue;

                MarkBefore(sr);

                // give ALL remaining to this skill; helper will stop at cap and return consumed amount
                float used = AddRawXpNoRate(sr, remaining, cap);
                remaining -= used;

                MarkAfter(sr);
            }

            if (changed.Count == 0) return string.Empty;

            // 6) Build string: " (Mining: 4.5 -> 5.2, Research: 3.2 -> 3.3)"
            var sb = new StringBuilder();
            sb.Append(" (");

            int written = 0;
            foreach (var kv in changed)
            {
                if (written > 0) sb.Append(", ");

                string label = kv.Key?.label ?? kv.Key?.defName ?? "스킬";
                sb.Append(label);
                sb.Append(": ");
                sb.Append(kv.Value.before.ToString("0.0"));
                sb.Append(" -> ");
                sb.Append(kv.Value.after.ToString("0.0"));

                written++;
            }

            sb.Append(")");
            return sb.ToString();
        }

        private static float AddRawXpNoRate(SkillRecord sr, float amount, int cap)
        {
            // returns how much XP was actually consumed
            if (sr == null || amount <= 0f) return 0f;
            if (sr.Level >= cap) return 0f;

            float consumed = 0f;

            while (amount > 0.0001f && sr.Level < cap)
            {
                float req = sr.XpRequiredForLevelUp;
                if (req <= 0.0001f) req = 1f;

                float need = req - sr.xpSinceLastLevel;
                if (need <= 0.0001f)
                {
                    // force level-up step (safety)
                    sr.xpSinceLastLevel = 0f;
                    sr.levelInt = Mathf.Min(sr.levelInt + 1, cap); // RimWorld field name is levelInt
                    continue;
                }

                float give = Mathf.Min(amount, need);

                // raw apply (NO learning rate)
                sr.xpSinceLastLevel += give;
                consumed += give;
                amount -= give;

                // handle level-up if reached
                if (sr.xpSinceLastLevel >= req - 0.0001f)
                {
                    sr.xpSinceLastLevel = 0f;
                    sr.levelInt = Mathf.Min(sr.levelInt + 1, cap);
                }
            }

            return consumed;
        }

        private static float SkillAsFloat(SkillRecord sr)
        {
            if (sr == null) return 0f;
            int cap = GameplayConstants.SkillLevelMax;

            if (sr.Level >= cap) return sr.Level;

            float req = sr.XpRequiredForLevelUp;
            if (req <= 0.0001f) return sr.Level;

            float frac = Mathf.Clamp01(sr.xpSinceLastLevel / req);
            return sr.Level + frac;
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