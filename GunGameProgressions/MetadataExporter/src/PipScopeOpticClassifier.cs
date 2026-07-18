namespace HLin.GunGameProgressions;

public static class PipScopeOpticClassifier
{
    // Stock H3VR catalog calls this PSO-1 scope "MagnifierPSO1" even though
    // it is a standalone 4x Russian side-rail scope. Normalize this one
    // vanilla catalog ID before applying the general magnifier exclusion.
    private const string Pso1ScopeObjectId = "MagnifierPSO1";

    public static string ClassifyFromMetadata(string objectId, string attachmentFeature)
    {
        if (IsPso1Scope(objectId) && attachmentFeature == "Magnification")
        {
            return "Scope";
        }

        if (!string.IsNullOrEmpty(objectId) &&
            objectId.IndexOf("magnifier", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Magnifier";
        }

        if (attachmentFeature == "Reflex")
        {
            return "Reflex";
        }

        return attachmentFeature == "Magnification" ? "Scope" : string.Empty;
    }

    public static string Classify(string objectId, bool hasPipScope, bool hasReflexSight)
    {
        if (IsPso1Scope(objectId) && hasPipScope)
        {
            return "Scope";
        }

        if (!string.IsNullOrEmpty(objectId) &&
            objectId.IndexOf("magnifier", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Magnifier";
        }

        if (hasPipScope)
        {
            return "Scope";
        }

        return hasReflexSight ? "Reflex" : string.Empty;
    }

    private static bool IsPso1Scope(string objectId)
    {
        return string.Equals(objectId, Pso1ScopeObjectId, System.StringComparison.Ordinal);
    }
}
