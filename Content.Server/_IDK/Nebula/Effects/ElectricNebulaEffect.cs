using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Serialization;

namespace Content.Server._IDK.Nebula.Effects;

public sealed partial class ElectricNebulaEffect : NebulaEffect
{
    private PowerReceiverSystem _receiver = default!;
    private ApcSystem _apc = default!;
    private EntityLookupSystem _lookup = default!;

    private readonly HashSet<EntityUid> _disabledReceivers = new();
    private readonly HashSet<EntityUid> _toggledApcs = new();

    public override void OnShuttleEntered(Entity<NebulaComponent> nebula, EntityUid shuttle, EntityManager entManager)
    {
        _receiver = entManager.System<PowerReceiverSystem>();
        _apc = entManager.System<ApcSystem>();
        _lookup = entManager.System<EntityLookupSystem>();

        var receivers = new HashSet<Entity<ApcPowerReceiverComponent>>();
        _lookup.GetGridEntities<ApcPowerReceiverComponent>(shuttle, receivers);

        foreach (var (uid, comp) in receivers)
        {
            if (comp.PowerDisabled)
                continue;

            _receiver.TryTogglePower(uid, false, comp);
            _disabledReceivers.Add(uid);
        }

        var apcs = new HashSet<Entity<ApcComponent>>();
        _lookup.GetGridEntities<ApcComponent>(shuttle, apcs);

        foreach (var (uid, comp) in apcs)
        {
            if (!comp.MainBreakerEnabled)
                continue;

            _apc.ApcToggleBreaker(uid, comp);
            _toggledApcs.Add(uid);
        }
    }

    public override void OnShuttleExited(Entity<NebulaComponent> nebula, EntityUid shuttle, EntityManager entManager)
    {
        foreach (var uid in _disabledReceivers)
        {
            if (entManager.TryGetComponent<ApcPowerReceiverComponent>(uid, out var comp))
                _receiver.TryTogglePower(uid, true, comp);
        }
        _disabledReceivers.Clear();

        foreach (var uid in _toggledApcs)
        {
            if (entManager.TryGetComponent<ApcComponent>(uid, out var comp) && !comp.MainBreakerEnabled)
                _apc.ApcToggleBreaker(uid, comp);
        }
        _toggledApcs.Clear();
    }
}
