using UnityEngine;

// Paints the sunset sky (a 360-degree panorama) with code.
public static class SkyFactory
{
    // The mist below the horizon. The fog uses the same colour, so distant things melt into it.
    public static readonly Color MistColor = new Color(0.52f, 0.40f, 0.37f);

    private static Texture2D texture;

    public static Texture2D Get()
    {
        if (texture != null) return texture;

        const int width = 512;
        const int height = 256;

        Color horizonGlow = new Color(1.00f, 0.60f, 0.30f);
        Color mauve = new Color(0.60f, 0.38f, 0.52f);
        Color zenith = new Color(0.10f, 0.13f, 0.28f);

        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            float elevation = (v - 0.5f) * 180f; // -90 = straight down, +90 = straight up

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;

                // Above the horizon: orange glow, then mauve, then deep blue
                Color above = Color.Lerp(horizonGlow, mauve, Smooth(0f, 14f, elevation));
                above = Color.Lerp(above, zenith, Smooth(10f, 70f, elevation));

                // Streaks of cloud low in the sky
                float c1 = Mathf.Sin(u * 6.2832f * 5f + elevation * 0.35f);
                float c2 = Mathf.Sin(u * 6.2832f * 9f - elevation * 0.5f + 1.7f);
                float cloud = Smooth(0.2f, 0.9f, 0.5f * c1 * c2 + 0.5f);
                float cloudMask = Smooth(2f, 6f, elevation) * (1f - Smooth(18f, 30f, elevation));
                Color cloudColor = above * 0.55f + new Color(0.12f, 0.08f, 0.18f, 0f);
                above = Color.Lerp(above, cloudColor, cloud * cloudMask * 0.6f);

                // Below the horizon: a sea of mist that gets darker with depth
                Color below = Color.Lerp(MistColor, MistColor * 0.45f, Smooth(0f, 60f, -elevation));

                Color color = Color.Lerp(below, above, Smooth(-5f, 5f, elevation));
                color.a = 1f;
                pixels[y * width + x] = color;
            }
        }

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Generated Sky";
        texture.wrapModeU = TextureWrapMode.Repeat;
        texture.wrapModeV = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}