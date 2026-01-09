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
        public string outcome;             // 최악/대박

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
                s.ratioScore = (s.value - s.expected)/s.expected;
                totalRatioScore += s.score;
            }
            luckScore = steps.Count == 0? 0f : totalRatioScore/steps.Count;
            outcome = LuckTier(luckScore);
        }
        private string LuckTier(float score)
        {
            if (score >= 80) return "대박";
            if (score >= 70) return "아주 좋음";
            if (score >= 55) return "좋음";
            if (score >= 45) return "평범";
            if (score >= 30) return "나쁨";
            if (score >= 20) return "아주 나쁨";
            return "최악";
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
        public TraceStep(string title, float score, float expected, float value, bool isInverse=false)
        {
            this.title = title;
            this.score = score;
            this.expected = expected;
            this.value = value;
            this.isInverse = isInverse;
        }
    }
}