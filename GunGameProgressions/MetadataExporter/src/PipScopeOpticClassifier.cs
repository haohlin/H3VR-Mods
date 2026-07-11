namespace HLin.GunGameProgressions;

public static class PipScopeOpticClassifier
{
    public static string ClassifyFromMetadata(string objectId, string attachmentFeature)
    {
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
}
