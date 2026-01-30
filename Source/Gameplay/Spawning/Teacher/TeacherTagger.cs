using Verse;

namespace CheeseProtocol
{
    internal static class TeacherTagger
    {
        public const string Tag = "CheeseProtocol_TeacherPawn";

        public static void Mark(Pawn p)
        {
            if (p == null) return;
            p.questTags ??= new System.Collections.Generic.List<string>();
            if (!p.questTags.Contains(Tag)) p.questTags.Add(Tag);
        }

        public static bool IsTeacher(Pawn p)
            => p?.questTags != null && p.questTags.Contains(Tag);
    }
}