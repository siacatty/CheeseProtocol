using Verse;

namespace CheeseProtocol
{
    internal static class BullyTagger
{
    public const string Tag = "CheeseProtocol_BullyPawn";

    public static void Mark(Pawn p)
    {
        if (p == null) return;
        p.questTags ??= new System.Collections.Generic.List<string>();
        if (!p.questTags.Contains(Tag)) p.questTags.Add(Tag);
    }

    public static bool IsBully(Pawn p)
        => p?.questTags != null && p.questTags.Contains(Tag);
}
}