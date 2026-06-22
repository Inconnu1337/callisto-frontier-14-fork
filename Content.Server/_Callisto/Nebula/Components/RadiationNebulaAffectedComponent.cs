namespace Content.Server._Callisto.Nebula.Components;

[RegisterComponent]
public sealed partial class RadiationNebulaAffectedComponent : Component
{
    public TimeSpan WallActivationTime;

    public bool WallsActivated;
}
