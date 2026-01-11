using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CheeseProtocol
{
    public class CheeseRollTrace
    {
        public CheeseCommand commandKey;          // "!보급"
        public string username;
        public float luckScore;             // "평균 행운 점수:"
        public List<TraceStep> steps = new();
        public List<string> traits = new();
        public List<string> hediffs = new();
        public string outcome;             // 최악/대박
        public bool IsValid()
        {
            return steps.Count > 0;
        }
        public CheeseRollTrace(String username, CheeseCommand command)
        {
            this.username = username.NullOrEmpty() ? "Unknown" : username;
            commandKey = command;
        }
        public void CalculateScore()
        {
            //luckScore = steps.Count == 0 ? 0f : steps.Average(s => s.score);
            //outcome = LuckTier(luckScore);
            float totalRatioScore = 0f;
            foreach(var s in steps)
            {
                //s.ratioScore = (s.value - s.expected)/s.expected;
                totalRatioScore += s.score;
            }
            luckScore = steps.Count == 0? 0f : totalRatioScore/steps.Count;
            outcome = LuckTier(luckScore);
        }
        private string LuckTier(float score)
        {
            if (score >= 0.85f) return "대박";       // 상위 ~7.5%
            if (score >= 0.6f)  return "아주 좋음";  // 상위 ~20%
            if (score >= 0.25f) return "좋음";
            if (score >= -0.25f) return "평범";
            if (score >= -0.6f)  return "나쁨";
            if (score >= -0.85f) return "아주 나쁨";
            return "최악";                           // 하위 ~7.5%
        }
    }

    public class TraceStep
    {
        public string title;
        public float score;
        public float ratioScore;
        public float expected;
        public float value;
        public bool isInverse;

        public TraceStep() { }
        public TraceStep(string title, bool isInverse=false)
        {
            this.title = title;
            //this.score = score;
            //this.expected = expected;
            //this.value = value;
            this.isInverse = isInverse;
        }
    }
}