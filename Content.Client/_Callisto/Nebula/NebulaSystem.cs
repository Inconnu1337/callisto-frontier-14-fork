using Robust.Client.Graphics;

namespace Content.Client._Callisto.Nebula;

public sealed class NebulaSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayMan.AddOverlay(new NebulaOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<NebulaOverlay>();
    }
}
