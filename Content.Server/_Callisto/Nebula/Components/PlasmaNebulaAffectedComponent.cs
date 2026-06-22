using Robust.Shared.Timing;

namespace Content.Server._Callisto.Nebula.Components;

[RegisterComponent]
public sealed partial class PlasmaNebulaAffectedComponent : Component
{
    public TimeSpan NextHeatTime;

    public TimeSpan NextDamageTime;

    public TimeSpan NextHazardCheckTime;
}
