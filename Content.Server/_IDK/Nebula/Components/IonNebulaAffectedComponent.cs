using Robust.Shared.GameStates;

namespace Content.Server._IDK.Nebula.Components;

[RegisterComponent]
public sealed partial class IonNebulaAffectedComponent : Component
{
    public Dictionary<EntityUid, float> OriginalThrusts = new();

    public TimeSpan NextStallTime;
    public TimeSpan EndStallTime;
    public bool IsStalling;

    public TimeSpan NextAngleShiftTime;
    public Angle RadarAngleOffset;
}
