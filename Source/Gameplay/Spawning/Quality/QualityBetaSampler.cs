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
            TraceStep step,
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

            float expectedT01 = alpha/(alpha+beta);
            
            float expectedValue = Mathf.Lerp(min, max, expectedT01);
            float score = LuckScore(value, min, max, alpha, beta, inverseQ);

            step.value = value;
            step.expected = expectedValue;
            step.score = score;

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


        static float LuckScore(
            float value,
            float min,
            float max,
            float alpha,
            float beta,
            bool isInverse)
        {
            const float eps = 1e-6f;

            // 1) value -> [0,1]
            float x = (value - min) / Mathf.Max(eps, (max - min));
            x = Mathf.Clamp01(x);

            if (isInverse)
                x = 1f - x;

            // 2) Beta 평균 / 분산
            float s  = alpha + beta;
            float mu = alpha / Mathf.Max(eps, s);

            // var = αβ / [ (α+β)^2 (α+β+1) ]
            float var = (alpha * beta) /
                        (Mathf.Max(eps, s * s) * Mathf.Max(eps, s + 1f));
            float sigma = Mathf.Sqrt(Mathf.Max(eps, var));

            // 3) z-score
            float z = (x - mu) / Mathf.Max(eps, sigma);

            // 4) 정규분포 퍼센타일 Φ(z)
            float phi = NormalCDF(z);

            // 5) [-1, +1] 스케일
            return Mathf.Clamp(2f * (phi - 0.5f), -1f, 1f);
        }

        static float NormalCDF(float z)
        {
            // Φ(z) ≈ 0.5 * (1 + erf(z / sqrt(2)))
            return 0.5f * (1f + Erf(z * 0.70710678f));
        }

        private static float Erf(float x)
        {
            // 최대 오차 ~1.5e-7
            float sign = Mathf.Sign(x);
            x = Mathf.Abs(x);

            //Abramowitz&Stegun constants
            float t = 1f / (1f + 0.3275911f * x);
            float y = 1f - (((((1.061405429f * t
                            - 1.453152027f) * t
                            + 1.421413741f) * t
                            - 0.284496736f) * t
                            + 0.254829592f) * t)
                            * Mathf.Exp(-x * x);

            return sign * y;
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