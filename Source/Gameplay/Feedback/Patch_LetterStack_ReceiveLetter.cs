using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CheeseProtocol.HarmonyPatches
{
    [HarmonyPatch]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_LetterStack_ReceiveLetter
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(LetterStack))
                .Where(m => m.Name == "ReceiveLetter");
        }

        static void Prefix(object[] __args)
        {
            if (!CheeseLetterContext.Active) return;

            var trace = CheeseLetterContext.Current;
            if (trace == null) return;

            if (__args == null || __args.Length == 0) return;

            // 1) Letter 객체가 있으면 그걸 최우선으로 수정 (가장 확실)
            if (TryAppendToLetterObject(__args, trace)) return;

            // 2) TaggedString(label/text 형태) 수정
            if (TryAppendToTaggedStringArg(__args, trace)) return;

            // 3) string(label/text 형태) 수정 (구버전/특수 오버로드 커버)
            TryAppendToStringArg(__args, trace);
        }

        private static bool TryAppendToLetterObject(object[] __args, CheeseRollTrace trace)
        {
            Letter letter = null;
            for (int i = 0; i < __args.Length; i++)
            {
                if (__args[i] is Letter l)
                {
                    letter = l;
                    break;
                }
            }
            if (letter == null) return false;

            // Letter 내부 text 필드/프로퍼티를 찾아서 수정
            // (버전/파생클래스 차 대비로 reflection)
            var textField =
                AccessTools.Field(letter.GetType(), "text") ??
                AccessTools.Field(typeof(Letter), "text");

            if (textField != null)
            {
                object cur = textField.GetValue(letter);

                string s = ExtractText(cur);
                if (s.NullOrEmpty()) return true;

                CheeseLetter.AppendToLetterText(ref s, trace);
                textField.SetValue(letter, RewrapText(cur, s));
                return true;
            }

            var textProp =
                AccessTools.Property(letter.GetType(), "Text") ??
                AccessTools.Property(typeof(Letter), "Text");

            if (textProp != null && textProp.CanRead)
            {
                object cur = textProp.GetValue(letter);

                string s = ExtractText(cur);
                if (s.NullOrEmpty()) return true;

                CheeseLetter.AppendToLetterText(ref s, trace);

                if (textProp.CanWrite)
                    textProp.SetValue(letter, RewrapText(cur, s));

                return true;
            }

            // text 접근 실패여도 Letter를 찾았다는 사실 자체는 true (더 이상 다른 브랜치로 안 내려감)
            if (Prefs.DevMode)
                Log.Warning($"[CheeseProtocol] ReceiveLetter got Letter={letter.GetType().FullName} but couldn't access text field/property.");
            return true;
        }

        private static bool TryAppendToTaggedStringArg(object[] __args, CheeseRollTrace trace)
        {
            int idx = FindLongestArgIndex(__args, preferTaggedString: true);
            if (idx < 0) return false;

            if (!(__args[idx] is TaggedString ts)) return false;

            string s = ts.RawText ?? ts.ToString();
            if (s.NullOrEmpty()) return true;

            CheeseLetter.AppendToLetterText(ref s, trace);
            __args[idx] = new TaggedString(s);
            return true;
        }

        private static bool TryAppendToStringArg(object[] __args, CheeseRollTrace trace)
        {
            int idx = FindLongestArgIndex(__args, preferTaggedString: false);
            if (idx < 0) return false;

            if (!(__args[idx] is string s)) return false;
            if (s.NullOrEmpty()) return true;

            CheeseLetter.AppendToLetterText(ref s, trace);
            __args[idx] = s;
            return true;
        }

        /// <summary>
        /// preferTaggedString=true이면 TaggedString 중 가장 긴 것,
        /// 아니면 string 중 가장 긴 것을 선택.
        /// </summary>
        private static int FindLongestArgIndex(object[] __args, bool preferTaggedString)
        {
            int bestIdx = -1;
            int bestLen = -1;

            for (int i = 0; i < __args.Length; i++)
            {
                if (preferTaggedString)
                {
                    if (__args[i] is TaggedString ts)
                    {
                        string s = ts.RawText ?? ts.ToString();
                        int len = s?.Length ?? 0;
                        if (len > bestLen) { bestLen = len; bestIdx = i; }
                    }
                }
                else
                {
                    if (__args[i] is string s)
                    {
                        int len = s?.Length ?? 0;
                        if (len > bestLen) { bestLen = len; bestIdx = i; }
                    }
                }
            }

            return bestIdx;
        }

        private static string ExtractText(object cur)
        {
            return cur switch
            {
                TaggedString ts => ts.RawText ?? ts.ToString(),
                string s => s,
                _ => cur?.ToString()
            };
        }

        private static object RewrapText(object original, string updated)
        {
            // 원래 타입 유지가 최우선
            if (original is TaggedString) return new TaggedString(updated);
            if (original is string) return updated;

            // 모르겠으면 TaggedString으로라도 넣어봄 (대부분 UI 텍스트는 TaggedString이 잘 먹음)
            return new TaggedString(updated);
        }
    }
}