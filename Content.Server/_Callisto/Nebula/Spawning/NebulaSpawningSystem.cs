using System.Linq;
using System.Numerics;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Systems;
using Content.Shared.EntityTable;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Callisto.Nebula;

public sealed class NebulaSpawningSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int NebulasPerStation = 20;

    private const float MinSpawnDistance = 4000f;
    private const float MaxSpawnDistance = 8000f;

    private const float MinNebulaSeparation = 600f;

    private const int MaxPlacementAttempts = 50;

    private readonly ProtoId<EntityTablePrototype> _nebulaSpawnTable = "CallistoNebulaSpawnTable";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<NebulaDespawningComponent, ComponentStartup>(OnDespawningStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var query = EntityQueryEnumerator<NebulaDespawningComponent, NebulaComponent>();
        while (query.MoveNext(out var uid, out var despawning, out var nebula))
        {
            if (now < despawning.DespawnAt)
                continue;

            Relocate(uid, despawning, nebula);
        }
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        foreach (var station in _station.GetStations())
        {
            SpawnNebulasForStation(station);
        }
    }

    private void OnDespawningStartup(EntityUid uid, NebulaDespawningComponent component, ComponentStartup args)
    {
        if (TryComp<NebulaComponent>(uid, out var nebula))
            component.DespawnAt = _timing.CurTime + nebula.DespawnTime;
    }

    private void SpawnNebulasForStation(EntityUid station)
    {
        if (!TryGetStationCenter(station, out var mapId, out var center))
            return;

        var table = _proto.Index<EntityTablePrototype>(_nebulaSpawnTable);
        var placedPositions = new List<Vector2>();

        for (var i = 0; i < NebulasPerStation; i++)
        {
            var spawns = _entityTable.GetSpawns(table).ToList();
            if (spawns.Count == 0)
                continue;

            var protoId = spawns[0];
            var position = FindValidPosition(center, placedPositions);
            placedPositions.Add(position);

            var coords = new MapCoordinates(position, mapId);
            var uid = Spawn(protoId, coords);

            var despawning = EnsureComp<NebulaDespawningComponent>(uid);
            despawning.Station = station;
        }
    }

    private void Relocate(EntityUid uid, NebulaDespawningComponent despawning, NebulaComponent nebula)
    {
        if (!TryGetStationCenter(despawning.Station, out var mapId, out var center))
        {
            despawning.DespawnAt = _timing.CurTime + nebula.DespawnTime;
            return;
        }

        var otherPositions = new List<Vector2>();
        var query = EntityQueryEnumerator<NebulaDespawningComponent, TransformComponent>();
        while (query.MoveNext(out var otherUid, out var otherDespawning, out var otherXform))
        {
            if (otherUid == uid || otherDespawning.Station != despawning.Station)
                continue;

            if (otherXform.MapID != mapId)
                continue;

            otherPositions.Add(_transform.GetWorldPosition(otherXform));
        }

        var newPosition = FindValidPosition(center, otherPositions);
        _transform.SetMapCoordinates(uid, new MapCoordinates(newPosition, mapId));

        despawning.DespawnAt = _timing.CurTime + nebula.DespawnTime;
    }

    private Vector2 FindValidPosition(Vector2 center, List<Vector2> existing)
    {
        var candidate = center;

        for (var attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            var distance = _random.NextFloat(MinSpawnDistance, MaxSpawnDistance);
            var offset = _random.NextAngle().RotateVec(new Vector2(distance, 0));
            candidate = center + offset;

            var farEnough = true;
            foreach (var other in existing)
            {
                if (Vector2.Distance(candidate, other) < MinNebulaSeparation)
                {
                    farEnough = false;
                    break;
                }
            }

            if (farEnough)
                break;
        }

        return candidate;
    }

    private bool TryGetStationCenter(EntityUid station, out MapId mapId, out Vector2 center)
    {
        mapId = default;
        center = default;

        if (!TryComp<Content.Server.Station.Components.StationDataComponent>(station, out var data))
            return false;

        if (_station.GetLargestGrid(data) is not { } grid)
            return false;

        if (!TryComp<TransformComponent>(grid, out var gridXform))
            return false;

        mapId = gridXform.MapID;
        center = _transform.GetWorldPosition(gridXform);

        return true;
    }
}
