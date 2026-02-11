using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static CheeseProtocol.CheeseLog;


namespace CheeseProtocol
{
    public class LordToil_Teacher_CatchStudents : LordToil
    {
        public Pawn teacher;
        private Pawn currentTarget;

        public List<Pawn> students;
        public int arrived;
        private LessonVenue venue;
        private IntVec3 teacherFaceDir;
        private int nextRotateTick = 0;
        private const int RotateInterval = 30;
        private const int GiveUpTicks = 600;
        private const int BreakTicks = 180;
        private const int UpdateVenueTicks = 600;
        private float d2Sq;
        private int nextGiveUpTick = 0;
        private int nextBreakTick = 0;
        private int nextBreakClassTick = 0;
        private int nextUpdateVenueTick = 0;

        public LordToilData_Gathering Data => (LordToilData_Gathering)data;

        public LordToil_Teacher_CatchStudents(Pawn teacher, List<Pawn> students, LessonVenue venue)
        {
            this.teacher = teacher;
            this.students = students;
            this.venue = venue;
            arrived = 0;
            data = new LordToilData_Gathering();
        }

        public override void Init()
        {
            base.Init();
            var job = (LordJob_Teacher)lord.LordJob;
            teacher = job.teacher;
            students = job.students;
            venue = job.currentVenue;
            if (!job.teacherSeat.IsValid || job.studentSeats == null || job.studentSeats.Count < students.Count)
                job.ReassignSeats();
            job.AddTeacherBuff();
            d2Sq = LordJob_Teacher.CatchDistance*LordJob_Teacher.CatchDistance;
            teacherFaceDir = job.teacherFaceDir;
            if (!teacher.Awake())
            {
                RestUtility.WakeUp(teacher);
            }
        }

        

        public override void LordToilTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextRotateTick) return;
            nextRotateTick = now + RotateInterval;
            IntVec3 spot = venue.spotCell;
            if (teacher == null) return;
            //Warn($"pawn counts = {pawns.Count}");
            var lj = lord?.LordJob as LordJob_Teacher;
            if (lj == null) return;


            List<Pawn> escapedPawnsToRemove = null;
            List<string> escapedUidsToRemove = null;
            List<string> stunnedUidsToRemove = null;
            List<string> uidsToClearEscaping = null;
            List<string> uidsToClearLost = null;

            foreach (var uid in lj.lostStudentUIDs)
            {
                Pawn pawn = null;
                if (!lj.lostCache.TryGetValue(uid, out pawn))
                {
                    pawn = TryResolvePawn(teacher, uid, lj.escapingStudentUIDs, checkSpawnOnly: false);
                    if (pawn != null)
                        lj.lostCache[uid] = pawn;
                }
                if (pawn == null) 
                {
                    Warn($"Cannot find student with uid: {uid}");
                    continue;
                }
                if (!pawn.Downed && pawn.Map == teacher.Map)
                {
                    uidsToClearLost ??= new List<string>(4);
                    uidsToClearLost.Add(uid);
                    lord.AddPawn(pawn);
                }
                else if (pawn.PositionHeld.DistanceToSquared(spot) > d2Sq)
                {
                    escapedPawnsToRemove ??= new List<Pawn>(4);
                    escapedUidsToRemove ??= new List<string>(4);
                    stunnedUidsToRemove ??= new List<string>(4);
                    uidsToClearLost ??= new List<string>(4);
                    escapedPawnsToRemove.Add(pawn);
                    stunnedUidsToRemove.Add(uid);
                    escapedUidsToRemove.Add(uid);
                    uidsToClearLost.Add(uid);
                    continue;
                }

            }

            var pawns = lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) return;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead) continue;
                if (pawn == teacher) continue;

                string uid = pawn.GetUniqueLoadID();
                if (uid.NullOrEmpty()) continue;

                bool inClass = LessonPosUtility.InGatheringArea(pawn.Position, spot, Map);
                bool inMental = pawn.InMentalState;
                bool isEscaping = lj.escapingStudentUIDs.Contains(uid);
                if (!inClass || inMental)
                {
                    lj.escapingStudentUIDs.Add(uid);
                }
                else
                {
                    if (isEscaping)
                    {
                        stunnedUidsToRemove ??= new List<string>(4);
                        uidsToClearEscaping ??= new List<string>(4);
                        uidsToClearEscaping.Add(uid);
                        stunnedUidsToRemove.Add(uid);
                    }
                }
                if (isEscaping && pawn.Position.DistanceToSquared(spot) > d2Sq)
                {
                    //Warn($"{pawn} to be removed from lord");
                    escapedPawnsToRemove ??= new List<Pawn>(4);
                    escapedUidsToRemove ??= new List<string>(4);
                    stunnedUidsToRemove ??= new List<string>(4);
                    escapedPawnsToRemove.Add(pawn);
                    stunnedUidsToRemove.Add(uid);
                    escapedUidsToRemove.Add(uid);
                    continue;
                }
                lj.studentSeats.TryGetValue(uid, out var seat2);
                // seat duty
                if (!pawn.Drafted && lj.studentSeats.TryGetValue(uid, out var seat))
                {
                    if (pawn.Position == seat)
                    {
                        if (teacher != null)
                        {
                            pawn.mindState.duty = new PawnDuty(DutyDefOf.Idle);
                            pawn.rotationTracker.FaceTarget(teacher);
                            pawn.rotationTracker.UpdateRotation();
                        }
                    }
                    else
                    {
                        pawn.mindState.duty = new PawnDuty(DutyDefOf.Goto, seat);
                    }
                }
            }
            if (escapedPawnsToRemove != null)
            {
                for (int i = escapedPawnsToRemove.Count - 1; i >= 0; i--)
                {
                    Pawn p = escapedPawnsToRemove[i];
                    lj.NotifyStudentEscape(p);
                    if (lj.currentTargetUid == p.GetUniqueLoadID()) lj.currentTargetUid = null;
                    if (lord.ownedPawns.Contains(p))
                    {
                        try
                        {
                            lord.Notify_PawnLost(p, PawnLostCondition.Undefined);
                        }
                        catch (System.Exception e)
                        {
                            Warn($"LordToil_Teacher_CatchStudents Notify_PawnLost failed: {e}");
                        }
                    }
                }
            }
            if (escapedUidsToRemove != null)
            {
                for (int i = 0; i < escapedUidsToRemove.Count; i++)
                    lj.escapingStudentUIDs.Remove(escapedUidsToRemove[i]);
            }

            if (stunnedUidsToRemove != null)
            {
                for (int i = 0; i < stunnedUidsToRemove.Count; i++)
                    lj.stunnedStudentUIDs.Remove(stunnedUidsToRemove[i]);
            }

            if (uidsToClearEscaping != null)
            {
                for (int i = 0; i < uidsToClearEscaping.Count; i++)
                    lj.escapingStudentUIDs.Remove(uidsToClearEscaping[i]);
            }

            if (uidsToClearLost != null)
            {
                for (int i = 0; i < uidsToClearLost.Count; i++)
                {
                    var uid = uidsToClearLost[i];
                    lj.lostStudentUIDs.Remove(uid);
                    lj.lostCache.Remove(uid);
                }
            }
            if (teacher != null)
            {
                bool chasingHolder;
                bool shoutWindowActive = now <= lj.shoutWindowUntilTick;
                bool shoutCDOff = now >= lj.nextShoutTick;
                bool canReachClass = teacher.CanReach(venue.spotCell, PathEndMode.Touch, Danger.Some);

                if (canReachClass)
                {
                    nextBreakClassTick = 0;
                }
                else
                {
                    if (nextBreakClassTick <= 0)
                        nextBreakClassTick = now + BreakTicks;
                    if (now >= nextBreakClassTick)
                    {
                        Thing toBreak = LessonPosUtility.FindWallToBreakNearTarget(teacher, Map, venue.spotCell, breakWallOnly: true) ??
                                        LessonPosUtility.FindWallToBreakNearTarget(teacher, Map, venue.spotCell, breakWallOnly: false);
                        if (toBreak == null)
                        {
                            if (nextUpdateVenueTick <= 0)
                                nextUpdateVenueTick = now + UpdateVenueTicks;
                            if (now >= nextUpdateVenueTick)
                            {
                                string text = LordChats.GetText(TeacherTextKey.PlayerCheat);
                                SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                                lord?.ReceiveMemo(LordJob_Teacher.MemoPlayerCheat);
                                nextUpdateVenueTick = 0;
                            }
                        }
                        else
                        {
                            nextBreakTick = 0;
                            nextUpdateVenueTick = 0;
                            if (CanStartBreakJobNow(teacher, toBreak))
                                StartBreakJob(teacher, toBreak);
                        }
                    }
                }

                if (shoutWindowActive || shoutCDOff)
                {
                    List<string> toStunUIDs= new List<string>(10);
                    foreach (var uid in lj.escapingStudentUIDs)
                    {
                        if (!lj.stunnedStudentUIDs.Contains(uid) && !lj.lostStudentUIDs.Contains(uid))
                        {
                            toStunUIDs.Add(uid);
                        }
                    }
                    if (toStunUIDs.Count > 0)
                    {
                        if (!shoutWindowActive && shoutCDOff)
                        {
                            var faceTarget = TryResolvePawn(teacher, toStunUIDs[0], lj.escapingStudentUIDs);
                            if (CanStartShoutJobNow(teacher, faceTarget))
                                StartShoutJob(teacher, faceTarget, LordJob_Teacher.ShoutTeacherDelayTicks);
                            lj.nextShoutTick = now + LordJob_Teacher.ShoutCooldown;
                            lj.shoutWindowUntilTick = now + LordJob_Teacher.ShoutWindowTicks;
                        }
                        foreach (var uid in toStunUIDs)
                        {
                            var p = TryResolvePawn(teacher, uid, lj.escapingStudentUIDs);
                            if (p == null) continue;
                            ApplyStun(teacher, p, LordJob_Teacher.StunDuration);
                            lj.stunnedStudentUIDs.Add(uid);
                        }
                    }
                }
                currentTarget = TryResolvePawnAndHolder(teacher, lj.currentTargetUid, lj.escapingStudentUIDs, out chasingHolder);
                if (RequireRetarget(teacher, currentTarget, lj.currentTargetUid, lj.escapingStudentUIDs, chasingHolder))
                {
                    string uid = null;
                    currentTarget = TryFindClosestEscapingStudent(teacher, lj.escapingStudentUIDs, out uid, excludeDowned: true)
                        ?? TryFindClosestEscapingStudent(teacher, lj.escapingStudentUIDs, out uid, excludeDowned: false);
                    lj.currentTargetUid = currentTarget == null? null : uid;
                }
                if (currentTarget!= null)
                {
                    lj.lastHasEscapeTick = now;
                    nextBreakTick = 0;
                    if (!currentTarget.Downed)
                    {
                        if (CanStartSubdueJobNow(teacher, currentTarget))
                            StartSubdueJob(teacher, currentTarget, spot);
                    }
                    else
                    {
                        if (CanStartCarryJobNow(teacher, currentTarget))
                            StartCarryJob(teacher, currentTarget, spot);
                    }
                }
                else
                {
                    if (lj.escapingStudentUIDs.Count == 0)
                    {
                        if (now - lj.lastHasEscapeTick >= LordJob_Teacher.ResumeLessonTicks)
                        {
                            string text = LordChats.GetText(TeacherTextKey.LessonResume);
                            SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker:SpeakerType.NonHostileNPC);
                            lord?.ReceiveMemo(LordJob_Teacher.MemoResumeLesson);
                        }
                    }
                    else
                    {
                        if (nextBreakTick <= 0)
                            nextBreakTick = now + BreakTicks;

                        if (now >= nextBreakTick)
                        {
                            Pawn target = TryFindClosestEscapingStudent(teacher, lj.escapingStudentUIDs, out _, checkReachable: false, excludeDowned: true) ??
                                        TryFindClosestEscapingStudent(teacher, lj.escapingStudentUIDs, out _, checkReachable: false, excludeDowned: false);
                            Thing toBreak = null;
                            if (target != null)
                            {
                                toBreak = LessonPosUtility.FindWallToBreakNearTarget(teacher, Map, target.Position, breakWallOnly: true) ??
                                            LessonPosUtility.FindWallToBreakNearTarget(teacher, Map, target.Position, breakWallOnly: false);
                            }
                            if (toBreak == null)
                            {
                                if (nextGiveUpTick <= 0)
                                    nextGiveUpTick = now + GiveUpTicks;
                                if (now >= nextGiveUpTick)
                                {
                                    string text = LordChats.GetText(TeacherTextKey.PlayerCheat);
                                    SpeechBubbleManager.Get(Map)?.AddNPCChat(text, teacher, speaker: SpeakerType.NonHostileNPC);
                                    lord?.ReceiveMemo(LordJob_Teacher.MemoResumeLesson);
                                    nextGiveUpTick = 0;
                                }
                            }
                            else
                            {
                                nextGiveUpTick = 0;
                                if (CanStartBreakJobNow(teacher, toBreak))
                                    StartBreakJob(teacher, toBreak);
                            }
                        }
                    }
                }
            }
        }

        private static void TryGiveDebuffHediff(Pawn student)
        {
            HediffDef debufDef = DefDatabase<HediffDef>.GetNamedSilentFail("CheeseProtocol_LessonDisciplineDebuff");
            if (debufDef != null)
            {
                TryAddHediff(student, debufDef);
            }
        }
        private static bool TryAddHediff(Pawn p, HediffDef def)
        {
            if (p == null || def == null) return false;
            if (p.health?.hediffSet == null) return false;
            if (p.health.hediffSet.HasHediff(def)) return false;
            p.health.AddHediff(def);
            return true;
        }


        public static void ApplyStun(Pawn teacher, Pawn target, int stunDuration)
        {
            if (teacher == null || target == null) return;
            target.stances?.stunner?.StunFor(stunDuration, teacher);
            if (target.jobs?.curJob != null)
            {
                target.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
            }
            TryGiveDebuffHediff(target);
        }

        private void StartBreakJob(Pawn teacher, Thing target)
        {
            if (teacher == null || target == null) return;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("CheeseProtocol_TeacherBreakWall");
            if (def == null) return;

            Job job = JobMaker.MakeJob(def, target);
            job.playerForced = true;
            teacher.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        }
        private bool CanStartBreakJobNow(Pawn teacher, Thing target)
        {
            if (teacher == null || target == null) return false;
            if (teacher.Downed || teacher.Dead) return false;

            var cur = teacher.CurJob;
            if (cur == null) return true;

            if (cur.def.defName == "CheeseProtocol_TeacherBreakWall"
                && cur.targetA.Thing == target)
                return false;

            return true;
        }

        private void StartShoutJob(Pawn teacher, Pawn target, int waitTicks=LordJob_Teacher.ShoutTeacherDelayTicks)
        {
            if (teacher == null || target == null) return;
            //Warn($"StartSubdue Job => {target}");
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("CheeseProtocol_TeacherShout");
            if (def == null) return;

            Job job = JobMaker.MakeJob(def, target);
            job.playerForced = true;
            job.count = waitTicks;
            teacher.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        }
        private bool CanStartShoutJobNow(Pawn teacher, Pawn target)
        {
            if (teacher == null || target == null) return false;
            if (teacher.Downed || teacher.Dead) return false;

            var cur = teacher.CurJob;
            if (cur == null) return true;
            if (cur.def.defName == "CheeseProtocol_TeacherBreakWall") return false;

            if (cur.def.defName == "CheeseProtocol_TeacherShout"
                && cur.targetA.Thing == target)
                return false;

            return true;
        }
        private void StartSubdueJob(Pawn teacher, Pawn target, IntVec3 spot)
        {
            if (teacher == null || target == null) return;
            //Warn($"StartSubdue Job => {target}");
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("CheeseProtocol_TeacherSubdue");
            if (def == null) return;

            Job job = JobMaker.MakeJob(def, target, spot);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = 1200;
            job.checkOverrideOnExpire = true;
            job.playerForced = true;

            teacher.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        }
        private bool CanStartSubdueJobNow(Pawn teacher, Pawn target)
        {
            if (teacher == null || target == null) return false;
            if (teacher.Downed || teacher.Dead) return false;

            var cur = teacher.CurJob;
            if (cur == null) return true;
            if (cur.def.defName == "CheeseProtocol_TeacherBreakWall") return false;
            if (cur.def.defName == "CheeseProtocol_TeacherShout") return false;
            if (cur.def.defName == "CheeseProtocol_TeacherSubdue"
                && cur.targetA.Thing == target)
                return false;

            return true;
        }
        private void StartCarryJob(Pawn teacher, Pawn target, IntVec3 spot)
        {
            if (teacher == null || target == null) return;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("CheeseProtocol_TeacherCarry");
            if (def == null) return;

            Job job = JobMaker.MakeJob(def, target, spot);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.count = 1;
            job.ignoreForbidden=true;
            job.expiryInterval = 2400;               // give up after 40s
            job.checkOverrideOnExpire = true;
            job.playerForced = true;

            teacher.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        }
        private bool CanStartCarryJobNow(Pawn teacher, Pawn target)
        {
            if (teacher == null || target == null) return false;
            if (teacher.Downed || teacher.Dead) return false;

            var cur = teacher.CurJob;
            if (cur == null) return true;
            if (cur.def.defName == "CheeseProtocol_TeacherBreakWall") return false;
            if (cur.def.defName == "CheeseProtocol_TeacherShout") return false;
            if (cur.def.defName == "CheeseProtocol_TeacherCarry"
                && cur.targetA.Thing == target)
                return false;

            return true;
        }
        private static bool RequireRetarget(Pawn teacher, Pawn target, string uid, HashSet<string> escapingStudentUIDs, bool chasingHolder)
        {
            if (teacher == null) return false;
            if (target == null || target.Destroyed || target.Dead || escapingStudentUIDs == null)
                return true;
            if (target.Spawned && teacher.Map != target.Map)
            {
                return true;
            }
            if (!teacher.CanReach(target, PathEndMode.Touch, Danger.Some))
                return true;
            if (uid.NullOrEmpty()) return true;
            if (!chasingHolder && !escapingStudentUIDs.Contains(uid)) return true;

            return false;
        }

        private Pawn TryResolvePawnAndHolder(Pawn teacher, string pawnUID, HashSet<string> escapingStudentUIDs, out bool chasingHolder)
        {
            chasingHolder = false;
            if (teacher == null || teacher.DestroyedOrNull() || teacher.Dead) return null;
            if (pawnUID.NullOrEmpty()) return null;

            if (!escapingStudentUIDs.Contains(pawnUID)) return null;

            var carriedPawn = teacher.carryTracker?.CarriedThing as Pawn;
            if (carriedPawn != null && !carriedPawn.DestroyedOrNull() && !carriedPawn.Dead)
            {
                string cuid = carriedPawn.GetUniqueLoadID();
                if (!cuid.NullOrEmpty() && cuid == pawnUID)
                    return carriedPawn;
            }

            Map map = teacher.Map;
            if (map == null) return null;

            var colonists = map.mapPawns?.FreeColonists;
            if (colonists == null || colonists.Count == 0) return null;

            Pawn found = null;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p == null || p.DestroyedOrNull() || p.Dead) continue;

                if (p.GetUniqueLoadID() == pawnUID)
                {
                    found = p;
                    break;
                }
            }

            if (found == null) return null;

            if (found.Spawned)
                return found;

            if (found.ParentHolder is Pawn_CarryTracker pct)
            {
                Pawn hp = pct.pawn;
                if (hp != null && !hp.DestroyedOrNull() && !hp.Dead && hp.Spawned)
                {
                    chasingHolder = true;
                    return hp;
                }
            }

            // 2) 혹시 ParentHolder가 직접 Pawn인 케이스도 커버
            if (found.ParentHolder is Pawn holderPawn)
            {
                if (!holderPawn.DestroyedOrNull() && !holderPawn.Dead && holderPawn.Spawned)
                {
                    chasingHolder = true;
                    return holderPawn;
                }
            }
            return null;
        }

        private Pawn TryResolvePawn(Pawn teacher, string pawnUID, HashSet<string> escapingStudentUIDs, bool checkSpawnOnly = true)
        {
            if (teacher == null || teacher.DestroyedOrNull() || teacher.Dead) return null;
            if (pawnUID.NullOrEmpty()) return null;

            if (!escapingStudentUIDs.Contains(pawnUID)) return null;

            Map map = teacher.Map;
            if (map == null) return null;
            List<Pawn> colonists = null;
            if (checkSpawnOnly)
                colonists = map.mapPawns?.FreeColonistsSpawned;
            else
                colonists = map.mapPawns?.FreeColonists;
            if (colonists == null || colonists.Count == 0) return null;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p == null || p.DestroyedOrNull() || p.Dead) continue;

                if (p.GetUniqueLoadID() == pawnUID)
                {
                    return p;
                }
            }
            return null;
        }

        private Pawn TryFindClosestEscapingStudent(
            Pawn teacher,
            HashSet<string> escapingStudentUIDs,
            out string bestPawnUID,
            bool checkReachable = true,
            bool excludeDowned = true)
        {
            bestPawnUID = null;

            if (teacher == null || teacher.DestroyedOrNull() || teacher.Dead) return null;
            if (escapingStudentUIDs == null || escapingStudentUIDs.Count == 0) return null;

            Map map = teacher.Map;
            if (map == null) return null;

            var colonists = map.mapPawns?.FreeColonists;
            if (colonists == null || colonists.Count == 0) return null;
            if (!excludeDowned)
            {
                var carriedPawn = teacher.carryTracker?.CarriedThing as Pawn;
                if (carriedPawn != null && !carriedPawn.DestroyedOrNull() && !carriedPawn.Dead)
                {
                    string cuid = carriedPawn.GetUniqueLoadID();
                    bestPawnUID = cuid;
                    return carriedPawn;
                }
            }

            IntVec3 tPos = teacher.Position;

            Pawn best = null;
            int bestDistSq = int.MaxValue;

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p == null || p.DestroyedOrNull() || p.Dead) continue;
                if (p == teacher) continue;

                string uid = p.GetUniqueLoadID();
                if (uid.NullOrEmpty()) continue;
                if (!escapingStudentUIDs.Contains(uid)) continue;
                if (checkReachable && !teacher.CanReach(p.PositionHeld, PathEndMode.Touch, Danger.Some)) continue;

                Pawn p2 = null;

                if (p.Spawned)
                {
                    p2 = p;
                }
                else
                {
                    if (p.ParentHolder is Pawn_CarryTracker pct)
                    {
                        Pawn hp = pct.pawn;
                        if (hp != null && !hp.DestroyedOrNull() && !hp.Dead && hp.Spawned)
                        {
                            p2 = hp;
                            if (p2 == teacher) continue;
                            if (checkReachable && !teacher.CanReach(p2, PathEndMode.Touch, Danger.Some)) continue;
                        }
                    }
                    else if (p.ParentHolder is Pawn holderPawn)
                    {
                        if (!holderPawn.DestroyedOrNull() && !holderPawn.Dead && holderPawn.Spawned)
                        {
                            p2 = holderPawn;
                            if (p2 == teacher) continue;
                            if (checkReachable && !teacher.CanReach(p2, PathEndMode.Touch, Danger.Some)) continue;
                        }
                    }
                }

                if (p2 == null) continue;
                if (excludeDowned && p2.Downed) continue;

                int dSq = p2.Position.DistanceToSquared(tPos);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = p2;
                    bestPawnUID = uid;

                    if (bestDistSq <= 1) break;
                }
            }
            return best;
        }
        
        public override void UpdateAllDuties()
        {
            var lj = lord?.LordJob as LordJob_Teacher;
            if (lj == null) return;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (!pawn.Awake())
                {
                    RestUtility.WakeUp(pawn);
                }

                if (pawn == teacher)
                {
                    PawnDuty teacherDuty;
                    if (pawn.Position != lj.teacherSeat)
                        teacherDuty = new PawnDuty(DutyDefOf.Goto, lj.teacherSeat);
                    else
                    {
                        teacherDuty = new PawnDuty(DutyDefOf.Idle);
                    }
                    pawn.mindState.duty = teacherDuty;
                }
                else
                {
                    if (lj.studentSeats.TryGetValue(pawn.GetUniqueLoadID(), out IntVec3 seat))
                    {
                        PawnDuty studentDuty;
                        if (pawn.Position != seat)
                            studentDuty = new PawnDuty(DutyDefOf.Goto, seat);
                        else
                        {
                            studentDuty = new PawnDuty(DutyDefOf.Idle);
                        }
                        pawn.mindState.duty = studentDuty;
                    }
                }
            }
        }
    }
}