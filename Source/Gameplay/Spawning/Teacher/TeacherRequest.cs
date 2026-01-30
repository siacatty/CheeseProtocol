using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public class TeacherRequest
    {
        public int studentCount;
        public int teachSkill;
        public float passionProb;
        public Pawn teacherPawn;

        public bool IsValid => studentCount > 0 && teachSkill > 0;

        public TeacherRequest()
        {
            studentCount = -1;
            teachSkill = -1;
            passionProb = -1;
        }

        public override string ToString()
        {
            return $"studentCount={studentCount}, teachSkill={teachSkill}, passionProb={passionProb:0.##}";
        }
    }
}