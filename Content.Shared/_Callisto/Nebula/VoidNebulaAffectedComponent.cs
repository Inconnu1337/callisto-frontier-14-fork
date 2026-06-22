using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Callisto.Nebula;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VoidNebulaAffectedComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<VoidNebulaSource> Sources = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public readonly partial record struct VoidNebulaSource
{
    [DataField]
    public NetEntity Nebula { get; init; }

    [DataField]
    public Vector2 Position { get; init; }

    [DataField]
    public float Radius { get; init; }

    public VoidNebulaSource(NetEntity nebula, Vector2 position, float radius)
    {
        Nebula = nebula;
        Position = position;
        Radius = radius;
    }
}
