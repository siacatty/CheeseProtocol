using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using System.Linq;
using Verse;
using RimWorld.BaseGen;

namespace CheeseProtocol
{
    public static class SupplyApplier
    {
        public static bool TryApplyValueHelper(SupplyRequest supply, float supplyValue, TechLevel techLevel)
        {
            if (supply.type == SupplyType.Weapon)
            {
                if (CheeseProtocolMod.SupplyWeaponCatalog == null)
                    return false;
                supply.techLevel = techLevel;
                List<SupplyCandidate> weaponCandidates =
                    CheeseProtocolMod.SupplyWeaponCatalog
                        .Where(c => c.techLevel == supply.techLevel)
                        .OrderBy(c => c.marketValue)
                        .ToList();
                if (weaponCandidates.Count == 0) //fallback
                {
                    Log.Warning($"[CheeseProtocol] Weapons with TechLevel={techLevel} not found. Forcing TechLevel.Medieval");
                    supply.techLevel = TechLevel.Medieval;
                    weaponCandidates =
                        CheeseProtocolMod.SupplyWeaponCatalog
                            .Where(c => c.techLevel == supply.techLevel)
                            .OrderBy(c => c.marketValue)
                            .ToList();
                    if (weaponCandidates.Count == 0)
                    {
                        Log.Warning($"[CheeseProtocol] Weapons with TechLevel={supply.techLevel} not found.");
                        return false;
                    }
                }
                float supplyBudget = supplyValue / GetPriceFactor(supply.weaponTier);
                SupplyCandidate chosen = default;
                bool found = false;

                for (int i = weaponCandidates.Count - 1; i >= 0; i--)
                {
                    var cand = weaponCandidates[i];
                    if (cand.marketValue <= supplyBudget)
                    {
                        chosen = cand;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // budget이 너무 낮으면 최저가로 강제
                    chosen = weaponCandidates[0];
                }

                
                supply.setChosen(chosen);
                supply.count = 1;
                supply.totalValue = chosen.marketValue * GetPriceFactor(supply.weaponTier);
            }
            else
            {
                supply.count = Mathf.Max(
                    1,
                    Mathf.RoundToInt(supplyValue / Mathf.Max(1f, supply.marketValue))
                );
                supply.totalValue = supply.count * supply.marketValue;
            }
            return true;
        }
        public static bool TryApplyTierHelper(SupplyRequest supply, float tier, int window = 1)
        {
            List<SupplyCandidate> supplyCandidates = supply.type switch
            {
                SupplyType.Food     => CheeseProtocolMod.SupplyFoodCatalog,
                SupplyType.Medicine => CheeseProtocolMod.SupplyMedCatalog,
                SupplyType.Drug     => CheeseProtocolMod.SupplyDrugCatalog,
                SupplyType.Weapon   => CheeseProtocolMod.SupplyWeaponCatalog,
                _ => null
            };
            if (supplyCandidates == null)
                return false;
            if (supply.type == SupplyType.Weapon)
            {
                int weaponTier = Mathf.FloorToInt(tier);
                supply.weaponTier = (QualityCategory) weaponTier;
                supply.isWeaponTierSet = true;
            }
            else
            {
                tier = Mathf.Clamp01(tier);
                var sorted = supplyCandidates
                .OrderBy(c => c.marketValue)
                .ToList();

                int count = sorted.Count;
                int center = Mathf.RoundToInt(tier * (count - 1));

                int min = Mathf.Max(0, center - window);
                int max = Mathf.Min(count - 1, center + window);

                int idx = Rand.RangeInclusive(min, max);
                supply.setChosen(sorted[idx]);
            }
            return true;
        }
        public static bool TryApplyTypeHelper(SupplyRequest supply, SupplyAdvancedSettings adv)
        {
            supply.allowedTypes.Clear();
            if (adv.allowFoodSupply)
                supply.allowedTypes.Add(SupplyType.Food);
            if (adv.allowMedSupply)
                supply.allowedTypes.Add(SupplyType.Medicine);
            if (adv.allowDrugSupply)
                supply.allowedTypes.Add(SupplyType.Drug);
            if (adv.allowWeaponSupply)
                supply.allowedTypes.Add(SupplyType.Weapon);
            if (supply.allowedTypes.Count == 0)
                return false;
            supply.type = supply.allowedTypes.RandomElement();
            return true;
        }
        public static void BuildCatalogSupplyCandidates(
            out List<SupplyCandidate> foodList,
            out List<SupplyCandidate> medList,
            out List<SupplyCandidate> drugList,
            out List<SupplyCandidate> weaponList
            )
        {
            foodList   = new List<SupplyCandidate>(256);
            medList    = new List<SupplyCandidate>(64);
            drugList   = new List<SupplyCandidate>(64);
            weaponList = new List<SupplyCandidate>(256);

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (IsFood(def))
                {
                    SupplyCandidate c = new SupplyCandidate(SupplyType.Food, def);
                    foodList.Add(c);
                }
                else if (IsMedicine(def))
                {
                    SupplyCandidate c = new SupplyCandidate(SupplyType.Medicine, def);
                    medList.Add(c);
                }
                else if (IsDrug(def))
                {
                    SupplyCandidate c = new SupplyCandidate(SupplyType.Drug, def);
                    drugList.Add(c);
                }
                else if (IsWeapon(def))
                {
                    SupplyCandidate c = new SupplyCandidate(SupplyType.Weapon, def);
                    weaponList.Add(c);
                }
            }
        }
        private static bool IsFood(ThingDef d) =>
            d.category == ThingCategory.Item &&
            (
                IsMeal(d)
                || (d.ingestible != null && (d.ingestible.foodType & FoodTypeFlags.Meal) != 0)
            );
        private static bool IsMedicine(ThingDef d)
        {
            return d.IsMedicine &&
                    d.category == ThingCategory.Item;
        }
        private static bool IsDrug(ThingDef d)
        {
            return d.IsDrug &&
                    d.ingestible != null &&
                    d.category == ThingCategory.Item;
        }

        private static bool IsWeapon(ThingDef d)
        {
            if (d.thingCategories == null)
                return false;

            return d.thingCategories.Any(c =>
                c != null &&
                c.defName != null &&
                c.defName.StartsWith("Weapons"));
        }
        private static bool IsMeal(ThingDef d)
        {
            if (d.category != ThingCategory.Item) return false;
            if (d.thingCategories == null) return false;

            return d.thingCategories.Any(tc => tc.defName == "FoodMeals");
        }
        private static void DumpCatalog(string name, List<SupplyCandidate> list)
        {
            Log.Message($"[CheeseProtocol] --- SupplyCatalog {name} ---");
            for (int i = 0; i < list.Count; i++)
                Log.Message(list[i]);
        }
        public static float GetPriceFactor(QualityCategory q)
        {
            switch (q)
            {
                case QualityCategory.Awful:      return 0.6f;
                case QualityCategory.Poor:       return 0.8f;
                case QualityCategory.Normal:     return 1.0f;
                case QualityCategory.Good:       return 1.2f;
                case QualityCategory.Excellent:  return 1.4f;
                case QualityCategory.Masterwork: return 1.7f;
                case QualityCategory.Legendary:  return 2.0f;
                default:                         return 1.0f;
            }
        }
    }
}