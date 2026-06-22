using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Callisto.Nebula.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RadiationNebulaWallSourceComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;
}
