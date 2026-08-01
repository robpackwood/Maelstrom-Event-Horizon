using UnityEngine;

namespace EventHorizon.Unity;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class ProceduralVisual : MonoBehaviour
{
    public Color Tint = Color.white;
    public float Radius = .5f;
    public int Sides = 16;
    SpriteRenderer renderer;

    void Awake()
    {
        renderer = GetComponent<SpriteRenderer>();
        renderer.sprite = MakeSprite();
        renderer.color = Tint;
    }

    void LateUpdate() => renderer.color = Tint;

    Sprite MakeSprite()
    {
        int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size - 1) / 2f;
        float edge = size * .46f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Vector2 p = new(x, y); float angle = Mathf.Atan2(p.y - center.y, p.x - center.x);
            float polygon = Mathf.Cos(Mathf.PI / Sides) / Mathf.Cos(Mathf.Repeat(angle + Mathf.PI / Sides, Mathf.PI * 2 / Sides) - Mathf.PI / Sides);
            float d = Vector2.Distance(p, center) / Mathf.Max(.01f, polygon * edge);
            float alpha = Mathf.Clamp01((1 - d) * 12);
            pixels[y * size + x] = new Color(1, 1, 1, alpha);
        }
        texture.SetPixels(pixels); texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size / (Radius * 2));
    }
}
