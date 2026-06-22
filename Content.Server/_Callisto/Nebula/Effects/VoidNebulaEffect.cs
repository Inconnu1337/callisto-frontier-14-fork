using Content.Shared.Shuttles.Systems;
using Content.Shared._Callisto.Nebula;
using Content.Shared.Shuttles.Components;

namespace Content.Server._Callisto.Nebula.Effects;

public sealed partial class VoidNebulaEffect : NebulaEffect
{
    private SharedRadarConsoleSystem _radar = default!;
    private EntityLookupSystem _lookup = default!;

    public override void OnShuttleEntered(Entity<NebulaComponent> nebula, EntityUid shuttle, EntityManager entManager)
    {
        _radar ??= entManager.System<SharedRadarConsoleSystem>();
        _lookup ??= entManager.System<EntityLookupSystem>();

        if (!entManager.TryGetComponent<TransformComponent>(nebula.Owner, out var xform))
            return;

        var transform = entManager.System<SharedTransformSystem>();
        var affected = entManager.EnsureComponent<VoidNebulaAffectedComponent>(shuttle);
        var source = new VoidNebulaSource(
            entManager.GetNetEntity(nebula.Owner),
            transform.GetWorldPosition(xform),
            nebula.Comp.Radius);

        affected.Sources.RemoveAll(existing => existing.Nebula == entManager.GetNetEntity(nebula.Owner));
        affected.Sources.Add(source);
        entManager.Dirty(shuttle, affected);

        var consoles = new HashSet<Entity<RadarConsoleComponent>>();
        _lookup.GetGridEntities<RadarConsoleComponent>(shuttle, consoles);

        foreach (var (uid, comp) in consoles)
        {
            _radar.SetJammed(uid, true, comp);
        }
    }

    public override void OnShuttleExited(Entity<NebulaComponent> nebula, EntityUid shuttle, EntityManager entManager)
    {
        if (!entManager.TryGetComponent<VoidNebulaAffectedComponent>(shuttle, out var affected))
            return;

        affected.Sources.RemoveAll(source => source.Nebula == entManager.GetNetEntity(nebula.Owner));

        if (affected.Sources.Count == 0)
        {
            entManager.RemoveComponent<VoidNebulaAffectedComponent>(shuttle);

            var consoles = new HashSet<Entity<RadarConsoleComponent>>();
            _lookup.GetGridEntities<RadarConsoleComponent>(shuttle, consoles);

            foreach (var (uid, comp) in consoles)
            {
                _radar.SetJammed(uid, false, comp);
            }
            return;
        }

        entManager.Dirty(shuttle, affected);
    }
}
