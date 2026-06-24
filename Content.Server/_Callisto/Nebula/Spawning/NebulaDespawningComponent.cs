using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Callisto.Nebula;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class NebulaDespawningComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DespawnAt;

    [DataField]
    public EntityUid Station;
}
