using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class H3VRAssetInspectionBatch
{
    private static readonly string[] InterestTerms =
    {
        "scope", "pip", "reticle", "lens", "magnification", "windage",
        "elevation", "zero", "parallax", "vignette", "chromatic", "camera",
        "material", "shader"
    };

    [Serializable]
    private sealed class AuditReport
    {
        public string format = "h3vr-unity-batch-audit-v1";
        public string unityVersion;
        public string bundleName;
        public AssetRecord[] assets;
    }

    [Serializable]
    private sealed class AssetRecord
    {
        public string name;
        public string type;
        public string[] componentTypes;
        public string shader;
        public string[] keywords;
        public string[] fields;
    }

    public static void Run()
    {
        var input = RequireEnvironment("H3VR_ASSET_INSPECTION_INPUT");
        var output = RequireEnvironment("H3VR_ASSET_INSPECTION_OUTPUT");
        var bundle = AssetBundle.LoadFromFile(input);
        if (bundle == null)
        {
            throw new InvalidOperationException("Unity could not load inspection bundle.");
        }

        try
        {
            var records = new List<AssetRecord>();
            foreach (var asset in bundle.LoadAllAssets())
            {
                records.Add(DescribeAsset(asset));
            }
            records.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            var report = new AuditReport
            {
                unityVersion = Application.unityVersion,
                bundleName = bundle.name,
                assets = records.ToArray()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("[H3VRAssetInspection] Wrote batch audit: " + output);
        }
        finally
        {
            bundle.Unload(false);
        }
    }

    private static AssetRecord DescribeAsset(UnityEngine.Object asset)
    {
        var record = new AssetRecord
        {
            name = asset.name,
            type = asset.GetType().FullName,
            componentTypes = new string[0],
            keywords = new string[0],
            fields = new string[0]
        };

        var material = asset as Material;
        if (material != null)
        {
            record.shader = material.shader == null ? string.Empty : material.shader.name;
            record.keywords = material.shaderKeywords;
        }

        var gameObject = asset as GameObject;
        if (gameObject != null)
        {
            var components = gameObject.GetComponentsInChildren<Component>(true);
            var componentTypes = new List<string>();
            var fields = new List<string>();
            foreach (var component in components)
            {
                if (component == null)
                {
                    componentTypes.Add("<missing-script>");
                    continue;
                }
                componentTypes.Add(component.GetType().FullName);
                if (IsInteresting(component.GetType().Name))
                {
                    fields.AddRange(DescribeInterestingFields(component));
                }
            }
            componentTypes.Sort(StringComparer.Ordinal);
            fields.Sort(StringComparer.Ordinal);
            record.componentTypes = componentTypes.ToArray();
            record.fields = fields.ToArray();
        }
        return record;
    }

    private static IEnumerable<string> DescribeInterestingFields(Component component)
    {
        var fields = new List<string>();
        var serialized = new SerializedObject(component);
        var property = serialized.GetIterator();
        var enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!IsInteresting(property.propertyPath))
            {
                continue;
            }
            fields.Add(property.propertyPath + "=" + DescribeProperty(property));
        }
        return fields;
    }

    private static string DescribeProperty(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
                return property.intValue.ToString();
            case SerializedPropertyType.Float:
                return property.floatValue.ToString("R");
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == null ? "null" : property.objectReferenceValue.name;
            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();
            case SerializedPropertyType.Color:
                return property.colorValue.ToString();
            default:
                return property.propertyType.ToString();
        }
    }

    private static bool IsInteresting(string value)
    {
        value = value.ToLowerInvariant();
        foreach (var term in InterestTerms)
        {
            if (value.Contains(term))
            {
                return true;
            }
        }
        return false;
    }

    private static string RequireEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException("Missing required environment variable: " + name);
        }
        return value;
    }
}
