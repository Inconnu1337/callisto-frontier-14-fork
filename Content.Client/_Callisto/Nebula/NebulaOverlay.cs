using System.Numerics;
using Content.Shared._Callisto.Nebula;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Callisto.Nebula;

public sealed class NebulaOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private readonly SharedTransformSystem _transform = default!;

    private const float ShipPadding = 1.0f;
    private const float MinFalloffWidth = 4.0f;
    private const float VisibleRadius = 140f;
    private const float FadeRadius = 260f;
    private const float ShuttleSafeDistance = 4f;
    private const float FadeInPerSecond = 1.4f;
    private const float FadeOutPerSecond = 2.0f;

    private readonly ShaderInstance _shader;

    private float _drawAlpha;
    private Vector2 _drawShipCenter;
    private Vector2 _drawClearHalfSize;
    private Vector2 _drawFullHalfSize;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public NebulaOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypes.Index<ShaderPrototype>("VoidNebulaOverlay").InstanceUnique();
        _transform = _entManager.System<SharedTransformSystem>();
        ZIndex = 90;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var targetAlpha = 0f;

        var targetCenter = Vector2.Zero;
        var targetClear = new Vector2(VisibleRadius, VisibleRadius);
        var targetFull = new Vector2(FadeRadius, FadeRadius);

        if (TryGetTargetState(args, out var shipCenter, out var clearHalf, out var fullHalf, out var alpha))
        {
            targetAlpha = alpha;
            targetCenter = shipCenter;
            targetClear = clearHalf;
            targetFull = fullHalf;
        }

        var frameTime = (float)_timing.FrameTime.TotalSeconds;

        var fadeSpeed = targetAlpha > _drawAlpha ? FadeInPerSecond : FadeOutPerSecond;
        _drawAlpha += Math.Clamp(targetAlpha - _drawAlpha, -fadeSpeed * frameTime, fadeSpeed * frameTime);

        if (_drawAlpha <= 0.005f)
        {
            _drawAlpha = 0f;
            return false;
        }

        if (_drawAlpha <= frameTime * fadeSpeed)
        {
            _drawShipCenter = targetCenter;
            _drawClearHalfSize = targetClear;
            _drawFullHalfSize = targetFull;
        }
        else
        {
            var lerpFactor = Math.Clamp(6.0f * frameTime, 0f, 1f);
            _drawShipCenter = Vector2.Lerp(_drawShipCenter, targetCenter, lerpFactor);
            _drawClearHalfSize = Vector2.Lerp(_drawClearHalfSize, targetClear, lerpFactor);
            _drawFullHalfSize = Vector2.Lerp(_drawFullHalfSize, targetFull, lerpFactor);
        }

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _shader.SetParameter("clearHalfSize", _drawClearHalfSize);
        _shader.SetParameter("fullHalfSize", _drawFullHalfSize);
        _shader.SetParameter("centerOffset", _drawShipCenter);
        _shader.SetParameter("overlayAlpha", _drawAlpha);
        _shader.SetParameter("noiseStrength", 0.12f);
        _shader.SetParameter("noiseScale", 2.2f);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldAABB, Color.White);
        args.WorldHandle.UseShader(null);
    }

    private bool TryGetTargetState(
        OverlayDrawArgs args,
        out Vector2 centerOffset,
        out Vector2 clearHalfSize,
        out Vector2 fullHalfSize,
        out float alpha)
    {
        centerOffset = Vector2.Zero;
        clearHalfSize = default;
        fullHalfSize = default;
        alpha = 0f;

        if (_player.LocalEntity is not { } player ||
            !_entManager.TryGetComponent(player, out TransformComponent? playerXform) ||
            playerXform.MapID != args.MapId)
        {
            return false;
        }

        var playerWorldPos = _transform.GetWorldPosition(playerXform);
        var insideNebula = false;
        var nebulaRadius = 0f;

        if (_entManager.TryGetComponent(player, out VoidNebulaAffectedComponent? affected))
        {
            foreach (var source in affected.Sources)
            {
                var dist = (source.Position - playerWorldPos).Length();
                if (dist <= source.Radius)
                {
                    insideNebula = true;
                    nebulaRadius = MathF.Max(nebulaRadius, source.Radius);
                }
            }
        }

        if (!insideNebula)
            return false;

        EntityUid? targetGrid = null;

        if (playerXform.GridUid is { } playerGrid &&
            _entManager.HasComponent<VoidNebulaAffectedComponent>(playerGrid))
        {
            targetGrid = playerGrid;
        }
        else
        {
            var nearestDistance = float.MaxValue;
            var query = _entManager.EntityQueryEnumerator<VoidNebulaAffectedComponent, TransformComponent>();

            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (uid == player || xform.MapID != args.MapId)
                    continue;

                if (!_entManager.TryGetComponent(uid, out MapGridComponent? grid))
                    continue;

                var worldBounds = _transform.GetWorldMatrix(uid).TransformBox(grid.LocalAABB);
                var closestPoint = new Vector2(
                    Math.Clamp(playerWorldPos.X, worldBounds.Left, worldBounds.Right),
                    Math.Clamp(playerWorldPos.Y, worldBounds.Bottom, worldBounds.Top));

                var distance = Vector2.Distance(playerWorldPos, closestPoint);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    targetGrid = uid;
                }
            }

            if (nearestDistance > ShuttleSafeDistance)
                targetGrid = null;
        }

        if (targetGrid is { } gridUid && _entManager.TryGetComponent(gridUid, out MapGridComponent? gridComp))
        {
            var worldBounds = _transform.GetWorldMatrix(gridUid).TransformBox(gridComp.LocalAABB);

            var paddedWorldBounds = worldBounds.Enlarged(ShipPadding);

            var screenMin = _eye.WorldToScreen(paddedWorldBounds.BottomLeft);
            var screenMax = _eye.WorldToScreen(paddedWorldBounds.TopRight);
            var screenCenter = (screenMin + screenMax) * 0.5f;
            var screenSize = Vector2.Abs(screenMax - screenMin);

            var viewport = (Control)_eye.MainViewport;
            var viewportCenter = viewport.PixelSize / 2f;

            centerOffset = screenCenter - viewportCenter;
            clearHalfSize = screenSize * 0.5f;

            var falloffTiles = MathF.Max(MinFalloffWidth, nebulaRadius * 0.1f);
            var fullWorldBounds = paddedWorldBounds.Enlarged(falloffTiles);

            var screenFullMin = _eye.WorldToScreen(fullWorldBounds.BottomLeft);
            var screenFullMax = _eye.WorldToScreen(fullWorldBounds.TopRight);
            var screenFullSize = Vector2.Abs(screenFullMax - screenFullMin);

            fullHalfSize = screenFullSize * 0.5f;

            alpha = 1f;
            return true;
        }

        centerOffset = Vector2.Zero;
        clearHalfSize = new Vector2(VisibleRadius, VisibleRadius);
        fullHalfSize = new Vector2(FadeRadius, FadeRadius);
        alpha = 1f;
        return true;
    }
}
