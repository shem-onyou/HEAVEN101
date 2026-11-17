using UnityEngine;
using UnityEngine.UI;

// A character standing on screen breathes: the picture grows a tiny bit taller and back, very slowly,
// from its bottom edge. Only the drawn picture changes (its mesh), never its position, scale or color,
// so walking, jitter, coughing and tints from the scene scripts work exactly as before.
// It stops with the pause menu. Added by SceneAtmosphere to the characters' Images.
public class CharacterBreath : BaseMeshEffect
{
    private float period = 3.6f;
    private float phase;
    private float amount = 0.008f;
    private float lastTime = -1f;

    public void Setup(float breathPeriod, float breathAmount)
    {
        period = Mathf.Max(breathPeriod, 0.5f);
        amount = breathAmount;
        phase = Random.Range(0f, period);
    }

    private void Update()
    {
        // Only redraw while time moves (not while paused).
        if (graphic == null || !graphic.isActiveAndEnabled || Mathf.Approximately(Time.time, lastTime))
            return;

        lastTime = Time.time;
        graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        Rect rect = graphic.rectTransform.rect;
        float breath = Mathf.Sin((Time.time + phase) / period * Mathf.PI * 2f);
        float scaleY = 1f + amount * breath;
        float scaleX = 1f - amount * 0.35f * breath;
        float bottom = rect.yMin;
        float middle = rect.center.x;

        UIVertex vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            Vector3 position = vertex.position;
            position.x = middle + (position.x - middle) * scaleX;
            position.y = bottom + (position.y - bottom) * scaleY;
            vertex.position = position;
            vh.SetUIVertex(vertex, i);
        }
    }
}
