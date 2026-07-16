namespace HLin.GunGameProgressions;

// Loader state is read once per snapshot. It is deliberately separate from
// profile readiness: available catalog metadata is always eligible to build a
// candidate, while Complete only permits removal of a stale empty pair.
public enum ExternalContentLoadState
{
    Unavailable,
    Loading,
    Complete,
}
