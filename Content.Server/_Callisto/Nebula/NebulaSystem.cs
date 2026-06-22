using System.Linq;
using Content.Server._Callisto.Nebula.Components;
using Content.Server.Radio;
using Content.Server.Shuttles.Components;
using Content.Shared._Callisto.Nebula;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Callisto.Nebula;

public sealed class NebulaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private EntityQuery<ShuttleComponent> _shuttleQuery;

    private List<Entity<MapGridComponent>> _foundGrids = new();
    private readonly HashSet<EntityUid> _foundSet = new();

    public override void Initialize()
    {
        base.Initialize();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();

        SubscribeLocalEvent<NebulaComponent, ComponentShutdown>(OnNebulaShutdown);
        SubscribeLocalEvent<NebulaComponent, ShuttleEnteredNebulaEvent>(OnShuttleEnteredNebula);
        SubscribeLocalEvent<NebulaComponent, ShuttleExitedNebulaEvent>(OnShuttleExitedNebula);

        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
    }

    private void OnNebulaShutdown(EntityUid uid, NebulaComponent nebula, ComponentShutdown args)
    {
        foreach (var shuttle in nebula.ShuttlesInside)
        {
            var ev = new ShuttleExitedNebulaEvent(uid, shuttle);
            RaiseLocalEvent(uid, ref ev);
        }
        nebula.ShuttlesInside.Clear();
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<NebulaComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var nebula, out var xform))
        {
            foreach (var shuttle in nebula.ShuttlesInside)
            {
                foreach (var effect in nebula.Effects)
                    effect.Tick((uid, nebula), shuttle, EntityManager, frameTime);
            }

            if (curTime < nebula.NextCheck)
                continue;

            nebula.NextCheck = curTime + nebula.CheckInterval;

            if (xform.MapID == MapId.Nullspace)
                continue;

            UpdateNebula(uid, nebula, xform);
        }
    }

    private void UpdateNebula(EntityUid uid, NebulaComponent nebula, TransformComponent xform)
    {
        var worldPos = _transform.GetWorldPosition(xform);
        var shape = new PhysShapeCircle(nebula.Radius, worldPos);

        _foundGrids.Clear();
        _foundSet.Clear();
        _mapManager.FindGridsIntersecting(xform.MapID, shape, Robust.Shared.Physics.Transform.Empty, ref _foundGrids);

        foreach (var grid in _foundGrids)
        {
            _foundSet.Add(grid.Owner);

            if (!_shuttleQuery.HasComp(grid.Owner))
                continue;

            if (nebula.ShuttlesInside.Add(grid.Owner))
            {
                var ev = new ShuttleEnteredNebulaEvent(uid, grid.Owner);
                RaiseLocalEvent(uid, ref ev);
            }
        }

        nebula.ShuttlesInside.RemoveWhere(shuttle =>
        {
            if (_foundSet.Contains(shuttle) && _shuttleQuery.HasComp(shuttle))
                return false;

            var ev = new ShuttleExitedNebulaEvent(uid, shuttle);
            RaiseLocalEvent(uid, ref ev);
            return true;
        });

        var netNebula = GetNetEntity(uid);
        var source = new VoidNebulaSource(netNebula, worldPos, nebula.Radius);

        var playersInRange = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange(xform.MapID, worldPos, nebula.Radius, playersInRange,
            LookupFlags.Approximate | LookupFlags.Uncontained | LookupFlags.Contained);

        foreach (var (playerUid, _) in playersInRange)
        {
            var affected = EnsureComp<VoidNebulaAffectedComponent>(playerUid);
            affected.Sources.RemoveAll(s => s.Nebula == netNebula);
            affected.Sources.Add(source);
            Dirty(playerUid, affected);
            nebula.PlayersInside.Add(playerUid);
        }

        nebula.PlayersInside.RemoveWhere(player =>
        {
            if (playersInRange.Any(e => e.Owner == player))
                return false;

            if (!TryComp<VoidNebulaAffectedComponent>(player, out var affected))
                return true;

            affected.Sources.RemoveAll(s => s.Nebula == netNebula);
            if (affected.Sources.Count == 0)
                RemComp<VoidNebulaAffectedComponent>(player);
            else
                Dirty(player, affected);
            return true;
        });
    }

    public IEnumerable<Entity<NebulaComponent>> GetNebulaeContaining(EntityUid shuttleUid)
    {
        var query = EntityQueryEnumerator<NebulaComponent>();
        while (query.MoveNext(out var uid, out var nebula))
        {
            if (nebula.ShuttlesInside.Contains(shuttleUid))
                yield return (uid, nebula);
        }
    }

    private void OnShuttleEnteredNebula(EntityUid uid, NebulaComponent nebula, ShuttleEnteredNebulaEvent args)
    {
        foreach (var effect in nebula.Effects)
            effect.OnShuttleEntered((uid, nebula), args.Shuttle, EntityManager);
    }

    private void OnShuttleExitedNebula(EntityUid uid, NebulaComponent nebula, ShuttleExitedNebulaEvent args)
    {
        foreach (var effect in nebula.Effects)
            effect.OnShuttleExited((uid, nebula), args.Shuttle, EntityManager);
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        var source = Transform(args.RadioSource).Coordinates;

        var query = EntityQueryEnumerator<NebulaComponent, VoidNebulaComponent, TransformComponent>();
        while (query.MoveNext(out _, out var nebula, out var _, out var xform))
        {
            if (_transform.InRange(source, xform.Coordinates, nebula.Radius))
            {
                args.Cancelled = true;
                return;
            }
        }
    }
}
