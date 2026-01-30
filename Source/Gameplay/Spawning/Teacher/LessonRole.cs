using Verse;

namespace CheeseProtocol
{
    public class LessonRole
    {
        public string id;
        public TaggedString label;
        public int maxCount;

        public LessonRole(string id, TaggedString label, int maxCount = 999)
        {
            this.id = id;
            this.label = label;
            this.maxCount = maxCount;
        }

        public TaggedString Label => label;
        public TaggedString LabelCap => label.CapitalizeFirst();

        public static readonly LessonRole Student = new LessonRole("Student", "Students".Translate(), 999);
    }
}