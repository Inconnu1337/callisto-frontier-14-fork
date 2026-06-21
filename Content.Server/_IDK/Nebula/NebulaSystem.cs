using Content.Server.Shuttles.Components;
using Content.Shared._IDK.Nebula;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Timing;

namespace Content.Server._IDK.Nebula;

public sealed class NebulaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private EntityQuery<ShuttleComponent> _shuttleQuery;

    private List<Entity<MapGridComponent>> _foundGrids = new();
    private HashSet<EntityUid> _foundSet = new();

    public override void Initialize()
    {
        base.Initialize();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();
        SubscribeLocalEvent<NebulaComponent, ComponentShutdown>(OnNebulaShutdown);
        SubscribeLocalEvent<NebulaComponent, ShuttleEnteredNebulaEvent>(OnShuttleEnteredNebula);
        SubscribeLocalEvent<NebulaComponent, ShuttleExitedNebulaEvent>(OnShuttleExitedNebula);
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
}
