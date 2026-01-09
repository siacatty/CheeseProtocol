using UnityEngine;
using Verse;

namespace CheeseProtocol
{
    internal static class QualityEvaluator
    {
        public static float evaluateQuality(int amount, CheeseCommand cmd)
        {
            var settings = CheeseProtocolMod.Settings;

            if (!settings.TryGetCommandConfig(cmd, out var cfg))
                return 0f;
            if (amount == 0 ||(cfg.minDonation == cfg.maxDonation)) //Chat || min == max
            {
                return 0.5f;
            }

            return Evaluate(
                amount,
                cfg.minDonation,
                cfg.maxDonation,
                cfg.curve
            );

        }
        public static float Evaluate(int amount, int minDonation, int maxDonation, QualityCurve curve = QualityCurve.Linear)
        {
            amount = Mathf.Max(0, amount);
            minDonation = Mathf.Max(0, minDonation);
            maxDonation = Mathf.Max(minDonation + 1, maxDonation);
            float t;
            if (amount <= minDonation)
                t = 0f;
            else if (amount >= maxDonation)
                t = 1f;
            else
                t = (amount - minDonation) / (float)(maxDonation - minDonation);

            // 커브 적용
            return ApplyCurve01(t, curve);
        }

        private static float ApplyCurve01(float t, QualityCurve curve)
        {
            t = Mathf.Clamp01(t);

            switch (curve)
            {
                case QualityCurve.Linear:
                    return t;

                case QualityCurve.EaseOut2:
                    // 1 - (1 - t)^2
                    return 1f - (1f - t) * (1f - t);

                case QualityCurve.EaseOut3:
                    // 1 - (1 - t)^3
                    {
                        float u = 1f - t;
                        return 1f - u * u * u;
                    }
                case QualityCurve.Sqrt:
                    return Mathf.Sqrt(t);

                default:
                    return t;
            }
        }
        public static float SampleQualityWeighted(float quality, float std, float lowerTailAtMax, QualityRange weightRange, int baseMin, int baseMax)
        {
            quality = Mathf.Clamp01(quality);

            float min =  weightRange.qMin * baseMax;
            float max = weightRange.qMax * baseMax;

            if (max < min) max = min;

            float mean = Mathf.Lerp(min, max, quality);

            float noise = Rand.Gaussian();
            float noiseRaw = noise;
            if (noise < 0f)
            {
                float suppress = Mathf.Lerp(lowerTailAtMax, 1f, 1f - quality);
                noise *= suppress;
            }
            if (std > 0)
            {
                float minNoise = (min - mean) / std;
                float maxNoise = (max - mean) / std;
                noise = Mathf.Clamp(noise, minNoise, maxNoise);
            }
            float value = mean + noise * std;
            if (Prefs.DevMode)
            {
                Log.Message(
                    "[CheeseProtocol][QualitySample]\n" +
                    $" quality = {quality:F3}\n" +
                    $" range01 = [{weightRange.qMin:F2}, {weightRange.qMax:F2}]\n" +
                    $" baseRange = [{baseMin}, {baseMax}]\n" +
                    $" min/max = [{min:F2}, {max:F2}]\n" +
                    $" mean = {mean:F2}\n" +
                    $" std = {std:F2}\n" +
                    $" noise(raw) = {noiseRaw:F3}\n" +
                    $" noise(final) = {noise:F3}\n" +
                    $" value = {value:F2}"
                );
            }
            return value;
        }
    }
}