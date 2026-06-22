namespace Content.Shared._Callisto.Nebula;

[ByRefEvent]
public readonly record struct ShuttleEnteredNebulaEvent(EntityUid Nebula, EntityUid Shuttle);

[ByRefEvent]
public readonly record struct ShuttleExitedNebulaEvent(EntityUid Nebula, EntityUid Shuttle);
