using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    public static class CheeseLetter
    {

        // Settings 토글(너의 Settings 접근 방식에 맞게 바꿔)
        private static bool AppendEnabled =>
            CheeseProtocolMod.Settings != null && CheeseProtocolMod.Settings.appendRollLogToLetters;

        /// <summary>
        /// (1) 바닐라/타 모드가 만든 letterText에 "추가"만 하기 위한 함수.
        /// Harmony Postfix에서 ref string으로 호출.
        /// </summary>
        public static void AppendToLetterText(ref string letterText, CheeseRollTrace trace)
        {
            Log.Warning("[CheeseProtocol] AppendToLetterText called");
            if (!AppendEnabled) return;
            if (trace == null) return;
            if (letterText.NullOrEmpty()) return;

            string block = BuildTraceBlock(trace);
            if (block.NullOrEmpty()) return;

            letterText = letterText.TrimEnd() + "\n\n" + block;
        }

        /// <summary>
        /// (2) 내가 레터를 만들 때 baseText에 trace 블록을 붙인 텍스트를 얻기.
        /// </summary>
        public static string BuildWithAppend(string baseText, CheeseRollTrace trace)
        {
            if (!AppendEnabled || trace == null) return baseText ?? "";

            var t = (baseText ?? "").TrimEnd();

            var block = BuildTraceBlock(trace);
            if (block.NullOrEmpty()) return t;

            return t + "\n\n" + block;
        }

        public static void AlertFail(
            string command,
            string reason = "플레이어 정착지가 지정되지 않았습니다."
        )
        {
            Log.Warning("[CheeseProtocol] AlertFail called");
            Find.LetterStack.ReceiveLetter($"{command} 실패", reason, LetterDefOf.NegativeEvent);
        }
        public static void SendCheeseLetter(
            CheeseCommand command,
            string title,
            string baseText,
            LookTargets targets,
            CheeseRollTrace trace,
            Map map,
            LetterDef def)
        {
            string text = BuildWithAppend(baseText, trace);
            Find.LetterStack.ReceiveLetter(title, text, def, targets);
        }

        public static string BuildTraceBlock(CheeseRollTrace trace)
        {
            trace.CalculateScore();
            Log.Warning("[CheeseProtocol] BuildTraceBlock called");
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("─────────────────────────────────────────");
            sb.AppendLine($"평균 행운 점수: {Mathf.RoundToInt(trace.luckScore)}");
            const int titleWidth = 20;
            if (trace.steps != null && trace.steps.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("▼ 주사위 결과");

                foreach (var s in trace.steps)
                {
                    if (s == null || s.title.NullOrEmpty()) continue;

                    sb.Append($"- {s.title.PadRight(titleWidth)} : ");

                    float v = s.score;
                    bool isPositive = v >= 50f;
                    string color = isPositive ? "#2e8032ff" : "#aa4040ff";

                    string scoreText = $"{v:0.#}점";
                    sb.Append($"<color={color}>{scoreText}</color>");

                    sb.Append($"     (예상값: {s.expected:0.##} | 실제값: {s.value:0.##})");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            sb.AppendLine($"{trace.username}님의 운:  {trace.outcome}");
            sb.AppendLine("─────────────────────────────────────────");
            return sb.ToString().TrimEnd();
        }
    }
}