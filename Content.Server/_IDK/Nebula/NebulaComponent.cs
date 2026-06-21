using Content.Server._IDK.Nebula.Effects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._IDK.Nebula;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class NebulaComponent : Component
{
    [DataField]
    public float Radius = 200f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCheck;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(1.5);

    [ViewVariables]
    public HashSet<EntityUid> ShuttlesInside = new();

    [DataField]
    public List<NebulaEffect> Effects = new();
}
