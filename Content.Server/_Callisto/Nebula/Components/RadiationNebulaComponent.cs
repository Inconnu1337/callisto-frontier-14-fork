using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Callisto.Nebula.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RadiationNebulaComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Radiation", 2.0 },
        }
    };

    [DataField]
    public float DamageInterval = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDamageTime;

    [DataField]
    public TimeSpan WallActivationDelay = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan WallSourceDuration = TimeSpan.FromMinutes(10);

    [DataField]
    public float WallSourceIntensity = 3f;

    [DataField]
    public float WallSourceSlope = 0.5f;
}
