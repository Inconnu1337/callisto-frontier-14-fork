using System.Linq;
using Content.Server._Callisto.Nebula.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Callisto.Nebula.Effects;

public sealed partial class PlasmaNebulaEffect : NebulaEffect
{
    [DataField]
    public float AirHeatPerSecond = 2.5f;

    [DataField]
    public float EngineExplosionChance = 0.02f;

    [DataField]
    public float FireChance = 0.35f;

    [DataField]
    public float HeatInterval = 2f;

    [DataField]
    public float DamageInterval = 2f;

    [DataField]
    public float HazardCheckInterval = 10f;

    [DataField]
    public DamageSpecifier HullDamage = new()
    {
        DamageDict = new()
        {
            { "Structural", 1.0 },
        }
    };

    [DataField]
    public float HotspotExposeTemperature = 700f;

    [DataField]
    public float HotspotExposeVolume = 50f;

    [DataField]
    public float ExplosionTotalIntensity = 40f;

    [DataField]
    public float ExplosionSlope = 4f;

    [DataField]
    public float ExplosionMaxTileIntensity = 8f;

    [DataField]
    public string ExplosionType = "Default";

    private EntityLookupSystem _lookup = default!;
    private DamageableSystem _damageable = default!;
    private ExplosionSystem _explosion = default!;
    private AtmosphereSystem _atmosphere = default!;
    private SharedTransformSystem _xform = default!;
    private IGameTiming _timing = default!;
    private IRobustRandom _random = default!;
    private TagSystem _tag = default!;

    private void EnsureSystems(EntityManager entManager)
    {
        _lookup ??= entManager.System<EntityLookupSystem>();
        _damageable ??= entManager.System<DamageableSystem>();
        _explosion ??= entManager.System<ExplosionSystem>();
        _atmosphere ??= entManager.System<AtmosphereSystem>();
        _xform ??= entManager.System<SharedTransformSystem>();
        _timing ??= IoCManager.Resolve<IGameTiming>();
        _random ??= IoCManager.Resolve<IRobustRandom>();
        _tag ??= entManager.System<TagSystem>();
    }

    public override void OnShuttleEntered(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager)
    {
        EnsureSystems(entManager);

        var plasmaComp = entManager.EnsureComponent<PlasmaNebulaAffectedComponent>(shuttle);
        var curTime = _timing.CurTime;
        plasmaComp.NextHeatTime = curTime + TimeSpan.FromSeconds(HeatInterval);
        plasmaComp.NextDamageTime = curTime + TimeSpan.FromSeconds(DamageInterval);
        plasmaComp.NextHazardCheckTime = curTime + TimeSpan.FromSeconds(HazardCheckInterval);
    }

    public override void OnShuttleExited(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager)
    {
        entManager.RemoveComponentDeferred<PlasmaNebulaAffectedComponent>(shuttle);
    }

    public override void Tick(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager,
        float frameTime)
    {
        if (!entManager.TryGetComponent<PlasmaNebulaAffectedComponent>(shuttle, out var plasmaComp))
            return;

        EnsureSystems(entManager);

        var curTime = _timing.CurTime;

        if (curTime >= plasmaComp.NextHeatTime)
        {
            plasmaComp.NextHeatTime = curTime + TimeSpan.FromSeconds(HeatInterval);

            foreach (var mixture in _atmosphere.GetAllMixtures(shuttle, excite: true))
            {
                mixture.Temperature += AirHeatPerSecond * HeatInterval;
            }
        }

        if (curTime >= plasmaComp.NextDamageTime)
        {
            plasmaComp.NextDamageTime = curTime + TimeSpan.FromSeconds(DamageInterval);

            var walls = new HashSet<Entity<DamageableComponent>>();
            _lookup.GetGridEntities(shuttle, walls);

            foreach (var wall in walls)
            {
                if (!_tag.HasTag(wall.Owner, "Wall"))
                    continue;

                _damageable.TryChangeDamage(wall.Owner, HullDamage, ignoreResistances: false, interruptsDoAfters: false);
            }
        }

        if (curTime >= plasmaComp.NextHazardCheckTime)
        {
            plasmaComp.NextHazardCheckTime = curTime + TimeSpan.FromSeconds(HazardCheckInterval);

            if (_random.Prob(FireChance))
                TryIgnitePowerTile(shuttle, entManager);

            if (_random.Prob(EngineExplosionChance))
                TryExplodeThruster(shuttle, entManager);
        }
    }

    private void TryIgnitePowerTile(EntityUid shuttle, EntityManager entManager)
    {
        var powerReceivers = new HashSet<Entity<ApcPowerReceiverComponent>>();
        _lookup.GetGridEntities(shuttle, powerReceivers);

        if (powerReceivers.Count == 0)
            return;

        var target = _random.PickAndTake(powerReceivers.ToList());
        var targetXform = entManager.GetComponent<TransformComponent>(target.Owner);
        var shuttleXform = entManager.GetComponent<TransformComponent>(shuttle);
        var tileIndices = _xform.GetGridTilePositionOrDefault((target.Owner, targetXform));
        var mixture = _atmosphere.GetTileMixture(shuttle, shuttleXform.MapUid, tileIndices, excite: true);

        if (mixture is not null && !mixture.Immutable)
        {
            mixture.AdjustMoles(Gas.Plasma, 5f);
            mixture.AdjustMoles(Gas.Oxygen, 5f);
        }

        _atmosphere.HotspotExpose(shuttle, tileIndices, HotspotExposeTemperature, HotspotExposeVolume, target.Owner, true);
    }

    private void TryExplodeThruster(EntityUid shuttle, EntityManager entManager)
    {
        var thrusters = new HashSet<Entity<ThrusterComponent>>();
        _lookup.GetGridEntities(shuttle, thrusters);

        if (thrusters.Count == 0)
            return;

        var target = _random.PickAndTake(thrusters.ToList());
        var targetXform = entManager.GetComponent<TransformComponent>(target.Owner);
        var mapCoords = _xform.GetMapCoordinates(targetXform);

        _explosion.QueueExplosion(
            mapCoords,
            ExplosionType,
            ExplosionTotalIntensity,
            ExplosionSlope,
            ExplosionMaxTileIntensity,
            target.Owner);
    }
}
