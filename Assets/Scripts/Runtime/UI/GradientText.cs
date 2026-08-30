using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // Tints text vertically, top colour to bottom colour.
    //
    // The design's headlines are gold gradients — "DINO RUSH" runs from near-white through gold
    // into a burnt orange, which is what makes them read as embossed metal rather than as
    // yellow text. uGUI's Text has one flat colour, so the gradient has to be applied to the
    // generated mesh. Combined with a Shadow component underneath, this is most of the design's
    // headline treatment for about thirty lines and no assets.
    [RequireComponent(typeof(Text))]
    public sealed class GradientText : BaseMeshEffect
    {
        public Color TopColor = Color.white;
        public Color BottomColor = Color.grey;

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0) return;

            var vertices = new List<UIVertex>();
            helper.GetUIVertexStream(vertices);

            // Gradient across the whole text block rather than per-character, so a headline
            // reads as one piece of metal instead of each letter repeating the ramp.
            float bottom = float.MaxValue, top = float.MinValue;
            for (int i = 0; i < vertices.Count; i++)
            {
                float y = vertices[i].position.y;
                if (y < bottom) bottom = y;
                if (y > top) top = y;
            }

            float height = top - bottom;
            if (height <= Mathf.Epsilon) return;

            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                float t = (vertex.position.y - bottom) / height;
                // Multiplied, not replaced, so Text.color still controls overall alpha and any
                // fade or disabled tint keeps working.
                vertex.color *= Color.Lerp(BottomColor, TopColor, t);
                vertices[i] = vertex;
            }

            helper.Clear();
            helper.AddUIVertexTriangleStream(vertices);
        }
    }
}
