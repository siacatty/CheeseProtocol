using UnityEngine;
using Verse;

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
            QualityRange range01,
            float concentration01,
            int baseMin,
            int baseMax,
            bool debugLog = false)
        {
            quality = Mathf.Clamp01(quality);
            concentration01 = Mathf.Clamp01(concentration01);

            // Map qMin/qMax (0..1) to numeric range
            float min = Mathf.Clamp01(range01.qMin) * baseMax;
            float max = Mathf.Clamp01(range01.qMax) * baseMax;
            if (max < min) max = min;

            // === concentration split (internal only) ===
            // Low quality: still some luck
            //float concLow  = Mathf.Lerp(0f, 12f, concentration01);
            // High quality: strong protection
            //float concHigh = Mathf.Lerp(0f, 12f, concentration01);

            // Quality-based protection
            //float conc = Mathf.Lerp(concLow, concHigh, quality);
            float conc = Mathf.Lerp(0f, 20f, concentration01);
            // Beta parameters
            // conc == 0 → alpha=beta=1 → uniform random
            float alpha = 1f + quality * conc;
            float beta  = 1f + (1f - quality) * conc;

            float t01 = SampleBeta01(alpha, beta);     // 0..1
            float value = Mathf.Lerp(min, max, t01);   // min..max
            value = Mathf.Clamp(value, baseMin, baseMax);

            if (debugLog && Prefs.DevMode)
            {
                Log.Message(
                    "[CheeseProtocol][BetaSample]\n" +
                    $" quality={quality:F3}\n" +
                    $" concentration01={concentration01:F2}\n" +
                    $" conc={conc:F2}\n" +
                    $" alpha={alpha:F3} beta={beta:F3}\n" +
                    $" range01=[{range01.qMin:F2},{range01.qMax:F2}] -> min/max=[{min:F2},{max:F2}]\n" +
                    $" t01={t01:F3} value={value:F2}"
                );
            }

            return value;
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