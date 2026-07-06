namespace ThePing
{
    public struct AudioRolloffPoint
    {
        public AudioRolloffPoint(float distance, float volume)
        {
            Distance = distance;
            Volume = volume;
        }

        public float Distance { get; }

        public float Volume { get; }
    }

    public static class AudioRolloffPolicy
    {
        public const float MaxDistance = 4500f;

        public static readonly AudioRolloffPoint[] Points =
        {
            new AudioRolloffPoint(0f, 1f),
            new AudioRolloffPoint(50f, 0.5f),
            new AudioRolloffPoint(100f, 0.3f),
            new AudioRolloffPoint(1000f, 0.125f),
            new AudioRolloffPoint(2500f, 0.05f)
        };
    }
}
