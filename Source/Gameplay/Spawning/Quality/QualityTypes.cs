using RimWorld;
using RimWorld.QuestGen;
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

        public static QualityRange init(float min = 0f, float max = 1f)
        {
            //min = Mathf.Clamp01(min);
            //max = Mathf.Clamp01(max);
            if (max < min) (min, max) = (max, min);

            return new QualityRange { qMin = min, qMax = max };
        }
        public float Mean()
        {
            return (qMin + qMax)/2f;
        }
        public float Expected(float v)
        {
            return Mathf.Lerp(qMin, qMax, v);
        }
        public override string ToString()
        {
            return $"{qMin} ~ {qMax}";
        }
    }
} 