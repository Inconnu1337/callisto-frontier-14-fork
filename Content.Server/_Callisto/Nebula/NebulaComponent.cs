using Content.Server._Callisto.Nebula.Effects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Callisto.Nebula;

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

    [ViewVariables]
    public HashSet<EntityUid> PlayersInside = new();

    [DataField]
    public List<NebulaEffect> Effects = new();
}
