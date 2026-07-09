using System;

namespace HLin.GunGameProgressions;

public sealed class EnemySpawnWeight
{
    public EnemySpawnWeight(int value, int multiplicity)
    {
        Value = value;
        Multiplicity = multiplicity;
    }

    public int Value { get; private set; }
    public int Multiplicity { get; private set; }
}

public static class EnemyWeightPolicy
{
    public static EnemySpawnWeight Resolve(RuntimeEnemyEntry enemy)
    {
        if (enemy == null)
        {
            throw new ArgumentNullException("enemy");
        }

        var value = EnemyValue(enemy);
        return new EnemySpawnWeight(value, SpawnMultiplicity(enemy, value));
    }

    private static int EnemyValue(RuntimeEnemyEntry enemy)
    {
        var operatorTier = GetOperatorTier(enemy.EnemyNameString);
        if (operatorTier == OperatorTier.Standard)
        {
            return 2;
        }

        if (operatorTier == OperatorTier.Advanced || operatorTier == OperatorTier.Apex)
        {
            return 1;
        }

        switch (enemy.EnemyNameString)
        {
            case "RW_Rot": return 8;
            case "M_Swat_Scout": return 5;
            case "M_MercWiener_Riflewiener": return 3;
            case "M_Swat_SpecOps": return 2;
            case "M_Swat_Heavy": return 1;
        }

        return IsCoreEnemyFamily(enemy.EnemyNameString)
            ? CoreSpawnWeight(enemy.DifficultyScore)
            : OtherSpawnWeight(enemy.DifficultyScore);
    }

    private static int CoreSpawnWeight(int score)
    {
        if (score <= 15) return 8;
        if (score <= 40) return 5;
        if (score <= 65) return 3;
        if (score <= 100) return 2;
        return 1;
    }

    private static int OtherSpawnWeight(int score)
    {
        return score <= 40 ? 2 : 1;
    }

    private static int SpawnMultiplicity(RuntimeEnemyEntry enemy, int value)
    {
        var operatorTier = GetOperatorTier(enemy.EnemyNameString);
        if (operatorTier == OperatorTier.Apex)
        {
            return 1;
        }

        if (operatorTier == OperatorTier.Standard || operatorTier == OperatorTier.Advanced)
        {
            return 2;
        }

        if (!IsCoreEnemyFamily(enemy.EnemyNameString))
        {
            return 1;
        }

        switch (value)
        {
            case 8: return 13;
            case 5: return 8;
            case 3: return 6;
            case 2: return 4;
            default: return 2;
        }
    }

    private static bool IsCoreEnemyFamily(string enemyNameString)
    {
        return enemyNameString.StartsWith("RW_", StringComparison.Ordinal) ||
            enemyNameString.StartsWith("M_Swat_", StringComparison.Ordinal) ||
            enemyNameString.StartsWith("M_MercWiener_", StringComparison.Ordinal) ||
            enemyNameString.StartsWith("Comperator_", StringComparison.Ordinal);
    }

    private static OperatorTier GetOperatorTier(string enemyNameString)
    {
        if (string.IsNullOrEmpty(enemyNameString) ||
            !enemyNameString.StartsWith("Comperator_", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorTier.None;
        }

        if (ContainsToken(enemyNameString, "Heavy_") ||
            ContainsToken(enemyNameString, "Tier5") ||
            ContainsToken(enemyNameString, "MixedHighTier"))
        {
            return OperatorTier.Apex;
        }

        if (ContainsToken(enemyNameString, "Tier4") ||
            ContainsToken(enemyNameString, "MixedMedTier") ||
            ContainsToken(enemyNameString, "Medium_Tier3"))
        {
            return OperatorTier.Advanced;
        }

        return OperatorTier.Standard;
    }

    private static bool ContainsToken(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private enum OperatorTier
    {
        None,
        Standard,
        Advanced,
        Apex,
    }
}
