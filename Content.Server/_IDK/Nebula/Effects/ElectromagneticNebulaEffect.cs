using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Serialization;

namespace Content.Server._IDK.Nebula.Effects;

public sealed partial class ElectromagneticNebulaEffect : NebulaEffect
{
    private SharedRadarConsoleSystem _radar = default!;
    private EntityLookupSystem _lookup = default!;

    public override void OnShuttleEntered(Entity<NebulaComponent> nebula, EntityUid shuttle, EntityManager entManager)
    {
        _radar = entManager.System<SharedRadarConsoleSystem>();
        _lookup = entManager.System<EntityLookupSystem>();

        var consoles = new HashSet<Entity<RadarConsoleComponent>>();
        _lookup.GetGridEntities<RadarConsoleComponent>(shuttle, consoles);

        foreach (var (uid, comp) in consoles)
        {
            _radar.SetJammed(uid, true, comp);
        }
    }

    public override void OnShuttleExited(Entity<NebulaComponent> nebula, EntityUid shuttle, EntityManager entManager)
    {
        _radar = entManager.System<SharedRadarConsoleSystem>();
        _lookup = entManager.System<EntityLookupSystem>();

        var consoles = new HashSet<Entity<RadarConsoleComponent>>();
        _lookup.GetGridEntities<RadarConsoleComponent>(shuttle, consoles);

        foreach (var (uid, comp) in consoles)
        {
            _radar.SetJammed(uid, false, comp);
        }
    }
}
