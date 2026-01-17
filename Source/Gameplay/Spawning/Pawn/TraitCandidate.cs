using System.Collections.Generic;
using System.Security.Policy;
using RimWorld;
namespace CheeseProtocol
{
    public struct TraitCandidate
    {
        public TraitDef def;
        public int degree;
        public string key;
        public string label;
        public float commonality;
        public TraitDef[] conflictTraits;
        public SkillDef[] conflictPassions;
        public string[] exclusionTags;
        public bool isSexualOrientation;
        public bool isCommonalityZero;
        public bool IsValid => !string.IsNullOrEmpty(key);

        public TraitCandidate(
            TraitDef def,
            int degree,
            string label,
            float commonality,
            TraitDef[] conflictTraits,
            SkillDef[] conflictPassions,
            string[] exclusionTags)
        {
            this.def = def;
            this.degree = degree;
            this.key = MakeKey(def, degree);
            this.label = label;
            this.commonality = commonality;
            this.conflictTraits = conflictTraits;
            this.conflictPassions = conflictPassions;
            this.exclusionTags = exclusionTags;
        }
        private static string MakeKey(TraitDef def, int degree) => $"{def.defName}({degree})";
    }
}