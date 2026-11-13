using System.Collections.Generic;
using UnityEngine;

// Paints the game's stone, wood, rock and water textures with code, so there are no
// image files to import. Each texture is made once and then reused.
public static class TextureFactory
{
    public enum Kind
    {
        StoneSlabs,
        StoneBlocks,
        Planks,
        Rock,
        Cliff,
        Water
    }

    private static readonly Dictionary<Kind, Texture2D> cache = new Dictionary<Kind, Texture2D>();

    public static void PreloadAll()
    {
        foreach (Kind kind in System.Enum.GetValues(typeof(Kind)))
        {
            Get(kind);
        }
    }

    public static Texture2D Get(Kind kind)
    {
        Texture2D texture;
        if (cache.TryGetValue(kind, out texture) && texture != null) return texture;

        texture = Build(kind);
        cache[kind] = texture;
        return texture;
    }

    private static int SizeFor(Kind kind)
    {
        switch (kind)
        {
            case Kind.StoneSlabs:
            case Kind.StoneBlocks:
            case Kind.Planks:
                return 256;
            default:
                return 128;
        }
    }

    private static Texture2D Build(Kind kind)
    {
        int size = SizeFor(kind);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;

            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                pixels[y * size + x] = Sample(kind, u, v, size);
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.name = "Generated " + kind;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 4;
        texture.SetPixels32(pixels);
        texture.Apply(true);
        return texture;
    }

    private static Color Sample(Kind kind, float u, float v, int size)
    {
        switch (kind)
        {
            case Kind.StoneSlabs:
                return SampleBlocks(u, v, 3, 4, false, 11, 1f,
                    new Color(0.34f, 0.31f, 0.29f), new Color(0.56f, 0.51f, 0.45f));
            case Kind.StoneBlocks:
                return SampleBlocks(u, v, 2, 4, true, 23, 0.3f,
                    new Color(0.45f, 0.40f, 0.34f), new Color(0.72f, 0.65f, 0.55f));
            case Kind.Planks:
                return SamplePlanks(u, v);
            case Kind.Rock:
                return SampleRock(u, v, size);
            case Kind.Cliff:
                return SampleCliff(u, v);
            default:
                return SampleWater(u, v);
        }
    }

    // ---------- Stone slabs and carved blocks ----------

    private static Color SampleBlocks(float u, float v, int cols, int rows, bool staggered, int seed,
        float moss, Color dark, Color light)
    {
        float rowPosition = v * rows;
        int rowIndex = Mathf.FloorToInt(rowPosition);
        float fy = rowPosition - rowIndex;

        float offset = staggered ? (rowIndex % 2) * 0.5f : Hash(rowIndex, 0, seed);

        float cellPosition = u * cols + offset;
        int cellIndex = Mathf.FloorToInt(cellPosition);
        float fx = cellPosition - cellIndex;
        int cellId = ((cellIndex % cols) + cols) % cols;

        float slabRandom = Hash(cellId, rowIndex, seed + 5);

        // Distance to the nearest joint, in texture units
        float dx = Mathf.Min(fx, 1f - fx) / cols;
        float dy = Mathf.Min(fy, 1f - fy) / rows;
        float edge = Mathf.Min(dx, dy);

        // Each stone gets its own shade, plus fine grain
        float grain = Fbm(u, v, 6, 4, seed);
        float shade = Mathf.Clamp01(0.25f + slabRandom * 0.5f + (grain - 0.5f) * 0.9f);
        Color stone = Color.Lerp(dark, light, shade);

        // Edges are chipped and darker
        stone *= 0.55f + 0.45f * Smooth(0.004f, 0.03f, edge);

        // Hairline cracks in some areas
        float crackLine = 1f - Smooth(0f, 0.018f, Mathf.Abs(Fbm(u, v, 5, 3, seed + 9) - 0.5f));
        float crackMask = Smooth(0.5f, 0.65f, Fbm(u, v, 3, 2, seed + 3));
        stone *= 1f - 0.55f * crackLine * crackMask;

        // Yellow-green moss collects along the joints
        float mossNoise = Fbm(u, v, 12, 3, seed + 31);
        float mossAmount = moss * (1f - Smooth(0f, 0.05f, edge)) * Smooth(0.35f, 0.6f, mossNoise);
        stone = Color.Lerp(stone, new Color(0.36f, 0.40f, 0.14f), mossAmount);

        // Dark mortar line
        float mortar = 1f - Smooth(0.0015f, 0.007f, edge);
        stone = Color.Lerp(stone, new Color(0.05f, 0.05f, 0.05f), mortar);

        stone.a = 1f;
        return stone;
    }

    // ---------- Wooden planks ----------

    private static Color SamplePlanks(float u, float v)
    {
        const int rows = 10;

        float rowPosition = v * rows;
        int rowIndex = Mathf.FloorToInt(rowPosition);
        float fy = rowPosition - rowIndex;

        // Two planks per row, joined at a random spot
        float jointPosition = Hash(rowIndex, 1, 41);
        float shifted = u - jointPosition;
        shifted -= Mathf.Floor(shifted);

        float half = shifted * 2f;
        int plankIndex = Mathf.FloorToInt(half);
        float fu = half - plankIndex;

        float dy = Mathf.Min(fy, 1f - fy) / rows;
        float du = Mathf.Min(fu, 1f - fu) / 2f;

        float plankRandom = Hash(rowIndex, plankIndex, 43);
        Color wood = Color.Lerp(
            new Color(0.30f, 0.19f, 0.10f),
            new Color(0.52f, 0.35f, 0.20f),
            0.1f + plankRandom * 0.8f);

        // Wavy grain lines
        float wobble = Fbm(u, v, 3, 3, 47);
        float grain = 0.5f + 0.5f * Mathf.Sin((v * rows * 3f + 4f * wobble) * Mathf.PI * 2f);
        wood *= 0.8f + 0.3f * grain + (Fbm(u, v, 24, 2, 53) - 0.5f) * 0.3f;

        // Worn edges, dark gaps between planks, and dark joints
        wood *= 0.8f + 0.2f * Smooth(0.004f, 0.02f, dy);
        wood = Color.Lerp(wood, new Color(0.04f, 0.03f, 0.02f), 1f - Smooth(0.0015f, 0.006f, dy));
        wood = Color.Lerp(wood, new Color(0.06f, 0.04f, 0.03f), 1f - Smooth(0.001f, 0.004f, du));

        wood.a = 1f;
        return wood;
    }

    // ---------- Rock and cliff ----------

    private static Color SampleRock(float u, float v, int size)
    {
        float n = Fbm(u, v, 4, 5, 61);
        Color rock = Color.Lerp(new Color(0.28f, 0.26f, 0.24f), new Color(0.62f, 0.56f, 0.48f), n);

        float crack = 1f - Smooth(0f, 0.02f, Mathf.Abs(Fbm(u, v, 6, 3, 67) - 0.5f));
        rock *= 1f - 0.5f * crack;

        float speck = Hash(Mathf.FloorToInt(u * size), Mathf.FloorToInt(v * size), 71);
        rock *= 0.92f + 0.16f * speck;

        rock.a = 1f;
        return rock;
    }

    private static Color SampleCliff(float u, float v)
    {
        float warp = Fbm(u, v, 4, 4, 83);
        float band = 0.5f + 0.5f * Mathf.Sin((v * 8f + warp * 2.5f) * Mathf.PI * 2f);
        float n = Fbm(u, v, 8, 4, 89);

        float t = Mathf.Clamp01(0.2f + band * 0.35f + n * 0.45f);
        Color cliff = Color.Lerp(new Color(0.20f, 0.18f, 0.18f), new Color(0.52f, 0.45f, 0.38f), t);

        cliff.a = 1f;
        return cliff;
    }

    // ---------- Water ----------

    private static Color SampleWater(float u, float v)
    {
        float a = Mathf.Sin((u * 3f + 0.25f * Mathf.Sin(v * Mathf.PI * 4f)) * Mathf.PI * 2f);
        float b = Mathf.Sin((v * 3f + 0.25f * Mathf.Sin(u * Mathf.PI * 6f)) * Mathf.PI * 2f);

        float ridge = 1f - Mathf.Abs(a * b);
        float light = Mathf.Pow(ridge, 6f) * 0.8f + Fbm(u, v, 4, 3, 97) * 0.2f;

        Color water = Color.Lerp(new Color(0.04f, 0.20f, 0.26f), new Color(0.30f, 0.62f, 0.70f), Mathf.Clamp01(light));
        water.a = 1f;
        return water;
    }

    // ---------- Noise that wraps around, so tiles join without seams ----------

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    private static float ValueNoise(float u, float v, int period, int seed)
    {
        float x = u * period;
        float y = v * period;

        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);

        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        int x1 = (x0 + 1) % period;
        int y1 = (y0 + 1) % period;
        x0 %= period;
        y0 %= period;

        float a = Hash(x0, y0, seed);
        float b = Hash(x1, y0, seed);
        float c = Hash(x0, y1, seed);
        float d = Hash(x1, y1, seed);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        int period = basePeriod;

        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(u, v, period, seed + i * 17) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            period *= 2;
        }

        return sum / total;
    }
}