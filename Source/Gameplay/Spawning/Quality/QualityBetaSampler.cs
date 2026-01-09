using System;
using UnityEngine;
using Verse;
using static CheeseProtocol.CheeseLog;
namespace CheeseProtocol
{
    internal static class QualityBetaSampler
    {
        /// <summary>
        /// Sample a value using a Beta distribution shaped by quality and a single concentration control.
        /// - quality: 0..1 (moves the distribution mean)
        /// - concentration01: 0..1 (0 = uniform random, 1 = very stable & protected)
        /// - range01: qMin/qMax in 0..1, mapped onto [0..baseMax]
        /// Returns a float (round/clamp outside).
        /// </summary>
        public static float SampleQualityWeightedBeta(
            float quality,
            QualityRange range,
            float concentration01,
            out float score,
            bool inverseQ = false,
            bool debugLog = false)
        {
            quality = Mathf.Clamp01(quality);
            if (inverseQ) quality = 1f - quality;
            concentration01 = Mathf.Clamp01(concentration01);

            // Map qMin/qMax (0..1) to numeric range
            //float min = Mathf.Clamp01(range01.qMin) * baseMax;
            //float max = Mathf.Clamp01(range01.qMax) * baseMax;
            float min = range.qMin;
            float max = range.qMax;
            if (max < min) max = min;
            float conc = Mathf.Lerp(0f, 20f, concentration01);
            float alpha = 1f + quality * conc;
            float beta  = 1f + (1f - quality) * conc;

            float t01 = SampleBeta01(alpha, beta);     // 0..1
            float value = Mathf.Lerp(min, max, t01);   // min..max
            float expected = Mathf.Lerp(min, max, quality);
            score = LuckScore(LuckDeltaSigned(value, expected, min, max, inverseQ));
            if (debugLog)
            {
                QMsg(
                    "BetaSample:\n" +
                    $" quality={quality:F3}\n" +
                    $" concentration01={concentration01:F2}\n" +
                    $" conc={conc:F2}\n" +
                    $" alpha={alpha:F3} beta={beta:F3}\n" +
                    $" range01=[{range.qMin:F2},{range.qMax:F2}] -> min/max=[{min:F2},{max:F2}]\n" +
                    $" t01={t01:F3} value={value:F2}",
                    Channel.Debug
                );
            }
            return value;
        }

        private static float LuckDeltaSigned(float v, float e, float min, float max, bool isInverse)
        {
            const float eps = 1e-6f;

            if (isInverse)
            {
                // 뒤집기: "작을수록 좋음"
                if (v <= e) return (e - v) / Mathf.Max(eps, (e - min));
                else        return -(v - e) / Mathf.Max(eps, (max - e));
            }
            else
            {
                if (v >= e) return (v - e) / Mathf.Max(eps, (max - e));
                else        return -(e - v) / Mathf.Max(eps, (e - min));
            }
        }
        private static float LuckScore(float deltaSigned)
        {
            return Mathf.Clamp(50f * (deltaSigned + 1f), 0, 100);
        }
        // ---------- Beta / Gamma helpers ----------

        public static float SampleBeta01(float alpha, float beta)
        {
            alpha = Mathf.Max(1e-6f, alpha);
            beta  = Mathf.Max(1e-6f, beta);

            float x = SampleGamma(alpha);
            float y = SampleGamma(beta);
            float sum = x + y;

            if (sum <= 0f) return 0.5f;
            return x / sum;
        }

        // Gamma(shape=k, scale=1)
        public static float SampleGamma(float k)
        {
            k = Mathf.Max(1e-6f, k);

            // k < 1: boost method
            if (k < 1f)
            {
                float u = Mathf.Max(1e-12f, Rand.Value);
                return SampleGamma(k + 1f) * Mathf.Pow(u, 1f / k);
            }

            // Marsaglia–Tsang
            float d = k - 1f / 3f;
            float c = 1f / Mathf.Sqrt(9f * d);

            while (true)
            {
                float z = Rand.Gaussian();
                float onePlus = 1f + c * z;
                if (onePlus <= 0f) continue;

                float v = onePlus * onePlus * onePlus;
                float u = Rand.Value;

                if (u < 1f - 0.0331f * z * z * z * z)
                    return d * v;

                if (Mathf.Log(u) < 0.5f * z * z + d * (1f - v + Mathf.Log(v)))
                    return d * v;
            }
        }
    }
}