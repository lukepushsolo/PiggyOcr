namespace PiggyOcr.Core.Internal;

internal readonly record struct DetBox(float X, float Y, float W, float H, float Score);

internal static class DetPostProcess
{
    public static List<DetBox> Find(float[] probs, int w, int h, OcrOptions options)
    {
        var mask = new bool[probs.Length];
        for (int i = 0; i < probs.Length; i++) mask[i] = probs[i] > options.DetThreshold;

        var seen = new bool[probs.Length];
        var queue = new int[probs.Length];
        var boxes = new List<DetBox>();

        for (int start = 0; start < probs.Length; start++)
        {
            if (!mask[start] || seen[start]) continue;

            int head = 0, tail = 0;
            queue[tail++] = start;
            seen[start] = true;
            int minX = w, maxX = -1, minY = h, maxY = -1, count = 0;
            float sum = 0;

            while (head < tail)
            {
                int idx = queue[head++];
                int x = idx % w, y = idx / w;
                count++;
                sum += probs[idx];
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                if (x > 0) Try(idx - 1);
                if (x < w - 1) Try(idx + 1);
                if (y > 0) Try(idx - w);
                if (y < h - 1) Try(idx + w);
            }

            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            float score = sum / count;
            if (count < 12 || bw < options.MinBoxSize || bh < options.MinBoxSize) continue;
            if (score < options.BoxThreshold) continue;

            float dist = bw * bh * options.UnclipRatio / (2f * (bw + bh));
            boxes.Add(new DetBox(minX - dist, minY - dist, bw + 2 * dist, bh + 2 * dist, score));

            void Try(int ni)
            {
                if (!seen[ni] && mask[ni])
                {
                    seen[ni] = true;
                    queue[tail++] = ni;
                }
            }
        }

        return boxes;
    }
}
