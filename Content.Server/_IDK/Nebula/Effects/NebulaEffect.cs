using Robust.Shared.Serialization;

namespace Content.Server._IDK.Nebula.Effects;

[ImplicitDataDefinitionForInheritors]
public abstract partial class NebulaEffect
{
    public virtual void OnShuttleEntered(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager)
    {
    }

    public virtual void OnShuttleExited(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager)
    {
    }

    public virtual void Tick(
        Entity<NebulaComponent> nebula,
        EntityUid shuttle,
        EntityManager entManager,
        float frameTime)
    {
    }
}
