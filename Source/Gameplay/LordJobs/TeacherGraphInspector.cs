using System;
using System.Collections;
using System.Reflection;
using Verse;
using Verse.AI.Group;

namespace CheeseProtocol
{
    internal static class TeacherGraphSurgery
    {
        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 캐시 (성능/안정)
        private static FieldInfo _toilsField;
        private static FieldInfo _transField;

        private static IList GetToilsList(StateGraph g)
            => GetIListFieldCached(g, ref _toilsField, typeof(LordToil));

        private static IList GetTransitionsList(StateGraph g)
            => GetIListFieldCached(g, ref _transField, typeof(Transition));

        private static IList GetIListFieldCached(object obj, ref FieldInfo cache, Type elemType)
        {
            if (obj == null) return null;
            if (cache != null)
            {
                var v = cache.GetValue(obj) as IList;
                if (v != null) return v;
                cache = null;
            }

            var t = obj.GetType();
            foreach (var f in t.GetFields(BF))
            {
                object val;
                try { val = f.GetValue(obj); }
                catch { continue; }

                if (val is not IList list || list.Count < 0) continue;

                // 비어있어도 타입 판별이 필요: generic arg로 판별
                var ft = f.FieldType;

                // 1) List<T>, HashSet<T> (IList는 아니지만), etc... 중 IList인 것만 잡음
                // ft가 generic이면 arg로 판단
                if (ft.IsGenericType)
                {
                    var ga = ft.GetGenericArguments();
                    if (ga.Length == 1 && elemType.IsAssignableFrom(ga[0]))
                    {
                        cache = f;
                        return list;
                    }
                }

                // 2) generic 정보가 없거나 특이 케이스: 내용물로 샘플링
                // (첫 8개만 확인)
                int sample = Math.Min(list.Count, 8);
                for (int i = 0; i < sample; i++)
                {
                    var e = list[i];
                    if (e == null) continue;
                    if (elemType.IsAssignableFrom(e.GetType()))
                    {
                        cache = f;
                        return list;
                    }
                }
            }

            return null;
        }

        public static void Dump(StateGraph g, string tag)
        {
            if (g == null)
            {
                Log.Warning($"[CheeseProtocol][TeacherGraph] {tag}: graph=null");
                return;
            }

            var toils = GetToilsList(g);
            var trans = GetTransitionsList(g);

            Log.Warning($"[CheeseProtocol][TeacherGraph] {tag}: toils={(toils?.Count ?? -1)} transitions={(trans?.Count ?? -1)}");

            int panicToil = CountPanicToils(toils);
            if (panicToil > 0)
                Log.Warning($"[CheeseProtocol][TeacherGraph] {tag}: FOUND PanicFleeToil x{panicToil}");

            int panicTrans = CountTransitionsToPanic(trans);
            if (panicTrans > 0)
                Log.Warning($"[CheeseProtocol][TeacherGraph] {tag}: FOUND Transitions->PanicFlee x{panicTrans}");
        }

        private static int CountPanicToils(IList toils)
        {
            if (toils == null) return 0;
            int c = 0;
            for (int i = 0; i < toils.Count; i++)
            {
                if (toils[i] is LordToil lt && lt.GetType().FullName == "RimWorld.LordToil_PanicFlee")
                    c++;
            }
            return c;
        }

        private static int CountTransitionsToPanic(IList trans)
        {
            if (trans == null) return 0;
            int c = 0;
            for (int i = 0; i < trans.Count; i++)
            {
                var tr = trans[i] as Transition;
                if (tr == null) continue;

                var target = GetLordToilField(tr, "target");
                if (target != null && target.GetType().FullName == "RimWorld.LordToil_PanicFlee")
                    c++;
            }
            return c;
        }

        private static LordToil GetLordToilField(Transition tr, string name)
        {
            try
            {
                var f = tr.GetType().GetField(name, BF);
                return f?.GetValue(tr) as LordToil;
            }
            catch { return null; }
        }

        // ✅ 실제로 제거까지 하고 싶으면 이걸 호출
        public static void StripPanicFlee(StateGraph g)
        {
            if (g == null) return;

            var toils = GetToilsList(g);
            var trans = GetTransitionsList(g);
            if (toils == null || trans == null) return;

            // PanicFlee toil 인스턴스들 찾기
            var panicToils = new System.Collections.Generic.HashSet<object>();
            for (int i = 0; i < toils.Count; i++)
            {
                var lt = toils[i] as LordToil;
                if (lt != null && lt.GetType().FullName == "RimWorld.LordToil_PanicFlee")
                    panicToils.Add(lt);
            }

            int removedT = 0;
            for (int i = trans.Count - 1; i >= 0; i--)
            {
                var tr = trans[i] as Transition;
                if (tr == null) continue;

                var target = GetLordToilField(tr, "target");
                if (target != null && (target.GetType().FullName == "RimWorld.LordToil_PanicFlee" || panicToils.Contains(target)))
                {
                    trans.RemoveAt(i);
                    removedT++;
                }
            }

            int removedToil = 0;
            for (int i = toils.Count - 1; i >= 0; i--)
            {
                var lt = toils[i] as LordToil;
                if (lt != null && (lt.GetType().FullName == "RimWorld.LordToil_PanicFlee" || panicToils.Contains(lt)))
                {
                    toils.RemoveAt(i);
                    removedToil++;
                }
            }

            Log.Warning($"[CheeseProtocol][TeacherGraph] StripPanicFlee: removedTransitions={removedT}, removedToils={removedToil}");
        }
    }
}