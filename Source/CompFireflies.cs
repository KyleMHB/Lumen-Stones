using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Fireflies : CompProperties
{
    public bool column;
    public CompProperties_Fireflies() => compClass = typeof(CompFireflies);
}

[StaticConstructorOnStartup]
public class CompFireflies : ThingComp
{
    private static Material dots;

    public override void PostDraw()
    {
        base.PostDraw();
        if (!CompAnimationToggle.On(parent)) return;
        bool column = ((CompProperties_Fireflies)props).column;
        dots ??= SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.85f, 1f, 0.12f));
        Vector3 centre = parent.DrawPos + parent.def.graphicData.drawOffset;
        centre.y += 0.01f;
        if (!column) centre.z -= 0.045f;
        float time = ((Find.TickManager?.TicksGame ?? 0) + parent.thingIDNumber % 600) / 60f;
        centre.y += 0.002f;
        for (int i = 0; i < (column ? 7 : 3); i++)
        {
            float phase = time * 0.8f + i * 2.094395f;
            var spot = centre + new Vector3(Mathf.Sin(phase) * (column ? 0.11f : 0.055f), 0f,
                column ? (i - 3) * 0.105f + Mathf.Cos(phase * 0.73f) * 0.025f : Mathf.Cos(phase * 0.73f) * 0.075f);
            DrawQuad(spot, 0.0225f, 0.0225f, dots);
        }
    }

    private static void DrawQuad(Vector3 position, float width, float height, Material material)
    {
        var transform = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(width, 1f, height));
        GenDraw.DrawMeshNowOrLater(MeshPool.plane10, transform, material, false);
    }
}
