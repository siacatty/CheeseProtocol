using UnityEngine;
namespace CheeseProtocol
{
    public enum QualityLever
    {
        Passion,
        Traits,
        SkillLevel,
        WorkDisables,
        Health,
        Apparel,
        Weapon
    }

    public struct QualityRange
    {
        public float qMin;
        public float qMax;

        public static QualityRange Normalized(float min, float max)
        {
            min = Mathf.Clamp01(min);
            max = Mathf.Clamp01(max);
            if (max < min) (min, max) = (max, min);

            return new QualityRange { qMin = min, qMax = max };
        }
    }
} 