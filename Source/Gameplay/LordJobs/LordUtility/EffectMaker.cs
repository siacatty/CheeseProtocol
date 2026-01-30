using RimWorld;
using Verse;
using Verse.Sound;

namespace CheeseProtocol
{
    public static class EffectMaker
    {
        public const string Sound_TerrorRoar = "Ability_TerrorRoar";
        public const string Effect_TerrorRoar = "TerrorRoar";
        public static void PlayFX(Pawn caster, LocalTargetInfo focus, string soundDefName, string effecterDefName)
        {
            if (caster?.Map == null) return;

            var s = DefDatabase<SoundDef>.GetNamedSilentFail(soundDefName);
            if (s != null)
                s.PlayOneShot(new TargetInfo(caster.Position, caster.Map));

            var ed = DefDatabase<EffecterDef>.GetNamedSilentFail(effecterDefName);
            if (ed != null)
            {
                var eff = ed.Spawn();

                TargetInfo a = focus.IsValid
                    ? focus.ToTargetInfo(caster.Map)
                    : new TargetInfo(caster.Position, caster.Map);

                // 대부분의 progressbar처럼 TargetInfo.Invalid로도 잘 돎
                eff.EffectTick(a, TargetInfo.Invalid);
                eff.Cleanup();
            }
        }
        public static void StartEffect(Pawn caster, LocalTargetInfo focus, ref Effecter effecter, string effecterDefName, ref int effectEndTick, int durationTicks = 30)
        {
            if (caster?.Map == null) return;
            var ed = DefDatabase<EffecterDef>.GetNamedSilentFail(effecterDefName);
            if (ed == null) return;
            // Effecter 생성
            effecter = ed.Spawn();

            effectEndTick = Find.TickManager.TicksGame + durationTicks;

            // 첫 틱
            effecter.EffectTick(
                focus.IsValid ? focus.ToTargetInfo(caster.Map) : new TargetInfo(caster.Position, caster.Map),
                TargetInfo.Invalid
            );
        }
    }
}