using System.Text;

namespace PiggyOcr.Core.Internal;

internal static class CtcDecoder
{
    /// <summary>Class 0 is CTC blank; classes 1..N map to dict[0..N-1].</summary>
    public static (string Text, float Confidence) Decode(float[] logits, int steps, int classes, string[] dict)
    {
        var sb = new StringBuilder(steps);
        int prev = 0;
        float sum = 0;
        int kept = 0;

        for (int t = 0; t < steps; t++)
        {
            int off = t * classes;
            int best = 0;
            float bestValue = float.NegativeInfinity;
            for (int c = 0; c < classes; c++)
            {
                float v = logits[off + c];
                if (v > bestValue)
                {
                    bestValue = v;
                    best = c;
                }
            }

            if (best != 0 && best != prev)
            {
                int di = best - 1;
                if ((uint)di < (uint)dict.Length) sb.Append(dict[di]);
                sum += bestValue;
                kept++;
            }
            prev = best;
        }

        return (sb.ToString(), kept > 0 ? sum / kept : 0);
    }
}
