namespace PiggyOcr.Core;

public enum ModelTier
{
    Tiny,
    Small,
    Medium,
}

public sealed record ModelFiles(ModelTier Tier, string Directory, string DetPath, string RecPath);

public static class ModelSet
{
    public const string DetFile = "det.onnx";
    public const string RecFile = "rec.onnx";

    public static IReadOnlyList<ModelFiles> Discover(string modelsRoot)
    {
        var found = new List<ModelFiles>();
        foreach (var tier in new[] { ModelTier.Tiny, ModelTier.Small, ModelTier.Medium })
        {
            var name = tier.ToString().ToLowerInvariant();
            var dir = Path.Combine(modelsRoot, name);
            var det = Path.Combine(dir, DetFile);
            var rec = Path.Combine(dir, RecFile);
            if (File.Exists(det) && File.Exists(rec))
                found.Add(new ModelFiles(tier, dir, det, rec));
        }
        return found;
    }

    public static ModelFiles? BestInstalled(string modelsRoot) =>
        Discover(modelsRoot)
            .OrderByDescending(m => m.Tier)
            .FirstOrDefault();
}
