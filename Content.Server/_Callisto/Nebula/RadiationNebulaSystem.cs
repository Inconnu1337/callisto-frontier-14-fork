using Content.Server._Callisto.Nebula.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Damage;
using Content.Shared.Radiation.Components;
using Content.Shared.Tag;
using Robust.Shared.Timing;

namespace Content.Server._Callisto.Nebula;

public sealed class RadiationNebulaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const float UpdateRate = 1f;
    private float _accumulator;

    private EntityQuery<ShuttleComponent> _shuttleQuery;

    private readonly HashSet<Entity<DamageableComponent>> _foundDamageable = new();
    private readonly HashSet<Entity<DamageableComponent>> _foundWalls = new();

    public override void Initialize()
    {
        base.Initialize();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateRate)
            return;

        _accumulator -= UpdateRate;

        var curTime = _timing.CurTime;

        IrradiateOutsideEntities(curTime);
        UpdateWallActivation(curTime);
        UpdateWallExpiration(curTime);
    }

    private void IrradiateOutsideEntities(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<RadiationNebulaComponent, NebulaComponent, TransformComponent>();

        while (query.MoveNext(out var _, out var radiation, out var nebula, out var xform))
        {
            if (curTime < radiation.NextDamageTime)
                continue;

            radiation.NextDamageTime = curTime + TimeSpan.FromSeconds(radiation.DamageInterval);

            if (xform.MapID == Robust.Shared.Map.MapId.Nullspace)
                continue;

            var worldPos = _xform.GetWorldPosition(xform);

            _foundDamageable.Clear();
            _lookup.GetEntitiesInRange(
                xform.MapID,
                worldPos,
                nebula.Radius,
                _foundDamageable,
                LookupFlags.Approximate | LookupFlags.Uncontained | LookupFlags.Contained);

            foreach (var entity in _foundDamageable)
            {
                if (!TryComp<TransformComponent>(entity.Owner, out var targetXform))
                    continue;

                if (targetXform.GridUid is { } gridUid && nebula.ShuttlesInside.Contains(gridUid))
                    continue;

                _damageable.TryChangeDamage(entity.Owner, radiation.Damage, ignoreResistances: false, interruptsDoAfters: false);
            }
        }
    }

    private void UpdateWallActivation(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<RadiationNebulaComponent, NebulaComponent>();

        while (query.MoveNext(out _, out var radiation, out var nebula))
        {
            foreach (var shuttle in nebula.ShuttlesInside)
            {
                if (!_shuttleQuery.HasComp(shuttle))
                    continue;

                var affected = EnsureComp<RadiationNebulaAffectedComponent>(shuttle);

                if (affected.WallActivationTime == default)
                    affected.WallActivationTime = curTime + radiation.WallActivationDelay;

                if (affected.WallsActivated || curTime < affected.WallActivationTime)
                    continue;

                affected.WallsActivated = true;
                ActivateWalls(shuttle, radiation, curTime);
            }
        }

        var affectedQuery = EntityQueryEnumerator<RadiationNebulaAffectedComponent>();
        while (affectedQuery.MoveNext(out var shuttleUid, out var _))
        {
            var stillInside = false;
            var nebulaQuery = EntityQueryEnumerator<RadiationNebulaComponent, NebulaComponent>();
            while (nebulaQuery.MoveNext(out _, out _, out var nebula))
            {
                if (nebula.ShuttlesInside.Contains(shuttleUid))
                {
                    stillInside = true;
                    break;
                }
            }

            if (!stillInside)
                RemCompDeferred<RadiationNebulaAffectedComponent>(shuttleUid);
        }
    }

    private void ActivateWalls(EntityUid shuttle, RadiationNebulaComponent radiation, TimeSpan curTime)
    {
        _foundWalls.Clear();
        _lookup.GetGridEntities(shuttle, _foundWalls);

        var expiresAt = curTime + radiation.WallSourceDuration;

        foreach (var wall in _foundWalls)
        {
            if (!_tag.HasTag(wall.Owner, "Wall"))
                continue;

            if (HasComp<RadiationSourceComponent>(wall.Owner))
                continue;

            var source = AddComp<RadiationSourceComponent>(wall.Owner);
            source.Intensity = radiation.WallSourceIntensity;
            source.Slope = radiation.WallSourceSlope;
            source.Enabled = true;

            var marker = AddComp<RadiationNebulaWallSourceComponent>(wall.Owner);
            marker.ExpiresAt = expiresAt;
        }
    }

    private void UpdateWallExpiration(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<RadiationNebulaWallSourceComponent>();

        while (query.MoveNext(out var uid, out var marker))
        {
            if (curTime < marker.ExpiresAt)
                continue;

            RemCompDeferred<RadiationNebulaWallSourceComponent>(uid);
            RemCompDeferred<RadiationSourceComponent>(uid);
        }
    }
}
