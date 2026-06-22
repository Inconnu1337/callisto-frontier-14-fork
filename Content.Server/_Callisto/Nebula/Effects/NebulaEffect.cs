namespace Content.Server._Callisto.Nebula.Effects;

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
