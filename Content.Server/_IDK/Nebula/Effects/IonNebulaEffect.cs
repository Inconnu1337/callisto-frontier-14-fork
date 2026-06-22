using Content.Server._IDK.Nebula.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._IDK.Nebula.Effects;

public sealed partial class IonNebulaEffect : NebulaEffect
{
    [DataField]
    public float SpeedReduction = 0.5f;

    [DataField]
    public float MinStallInterval = 10f;

    [DataField]
    public float MaxStallInterval = 25f;

    [DataField]
    public float MinStallDuration = 2f;

    [DataField]
    public float MaxStallDuration = 6f;

    [DataField]
    public float MinAngleShiftInterval = 5f;

    [DataField]
    public float MaxAngleShiftInterval = 20f;

    private EntityLookupSystem _lookup = default!;
    private IGameTiming _timing = default!;
    private IRobustRandom _random = default!;
    private PowerReceiverSystem _powerReceiver = default!;
    private ShuttleConsoleSystem _shuttleConsole = default!;
    private ThrusterSystem _thruster = default!;

    private void EnsureSystems(EntityManager entManager)
    {
        _lookup ??= entManager.System<EntityLookupSystem>();
        _powerReceiver ??= entManager.System<PowerReceiverSystem>();
        _shuttleConsole ??= entManager.System<ShuttleConsoleSystem>();
        _thruster ??= entManager.System<ThrusterSystem>();
        _timing ??= IoCManager.Resolve<IGameTiming>();
        _random ??= IoCManager.Resolve<IRobustRandom>();
    }

    private TimeSpan RandomSeconds(float min, float max)
    {
        return TimeSpan.FromSeconds(
            _random.NextFloat(min, max));
    }

    private void SetThrusterPower(
        IonNebulaAffectedComponent ionComp,
        EntityManager entManager,
        bool enabled)
    {
        foreach (var uid in ionComp.OriginalThrusts.Keys)
        {
            if (!entManager.TryGetComponent<ApcPowerReceiverComponent>(uid, out var receiver))
                continue;

            _powerReceiver.TryTogglePower(uid, enabled, receiver);
        }
    }

    public override void OnShuttleEntered(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager)
    {
        EnsureSystems(entManager);

        var ionComp = entManager.EnsureComponent<IonNebulaAffectedComponent>(shuttle);

        var thrusters = new HashSet<Entity<ThrusterComponent>>();
        _lookup.GetGridEntities(shuttle, thrusters);

        foreach (var (uid, thruster) in thrusters)
        {
            ionComp.OriginalThrusts[uid] = thruster.Thrust;

            _thruster.SetThrust(uid, thruster.Thrust * SpeedReduction, thruster);
        }

        var curTime = _timing.CurTime;

        ionComp.NextStallTime = curTime + RandomSeconds(MinStallInterval, MaxStallInterval);
        ionComp.NextAngleShiftTime = curTime + RandomSeconds(MinAngleShiftInterval, MaxAngleShiftInterval);
    }

    public override void OnShuttleExited(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager)
    {
        if (!entManager.TryGetComponent<IonNebulaAffectedComponent>(shuttle, out var ionComp))
            return;

        EnsureSystems(entManager);

        foreach (var (uid, originalThrust) in ionComp.OriginalThrusts)
        {
            if (!entManager.TryGetComponent<ThrusterComponent>(uid, out var thruster))
                continue;

            _thruster.SetThrust(uid, originalThrust, thruster);
        }

        if (ionComp.IsStalling)
            SetThrusterPower(ionComp, entManager, true);

        entManager.RemoveComponentDeferred<IonNebulaAffectedComponent>(shuttle);

        _shuttleConsole.RefreshShuttleConsoles(shuttle);
    }

    public override void Tick(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager,
        float frameTime)
    {
        if (!entManager.TryGetComponent<IonNebulaAffectedComponent>(shuttle, out var ionComp))
            return;

        EnsureSystems(entManager);

        var curTime = _timing.CurTime;

        if (curTime >= ionComp.NextAngleShiftTime)
        {
            ionComp.RadarAngleOffset = Angle.FromDegrees(_random.NextFloat(0f, 360f));
            ionComp.NextAngleShiftTime = curTime + RandomSeconds(MinAngleShiftInterval, MaxAngleShiftInterval);

            _shuttleConsole.RefreshShuttleConsoles(shuttle);
        }

        if (!ionComp.IsStalling &&
            curTime >= ionComp.NextStallTime)
        {
            ionComp.IsStalling = true;
            ionComp.EndStallTime = curTime + RandomSeconds(MinStallDuration, MaxStallDuration);

            SetThrusterPower(ionComp, entManager, false);
            return;
        }

        if (!ionComp.IsStalling ||
            curTime < ionComp.EndStallTime)
            return;

        ionComp.IsStalling = false;
        ionComp.NextStallTime = curTime + RandomSeconds(MinStallInterval, MaxStallInterval);

        SetThrusterPower(ionComp, entManager, true);
    }
}
