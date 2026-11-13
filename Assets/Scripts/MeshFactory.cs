using System.Collections.Generic;
using UnityEngine;

// Builds the 3D shapes used by the world with code: boxes and columns whose textures
// keep a constant size, rough faceted rocks, and tapered flame tongues.
public static class MeshFactory
{
    public const int RockVariants = 6;

    private static readonly Dictionary<Vector4, Mesh> boxes = new Dictionary<Vector4, Mesh>();
    private static readonly Dictionary<Vector4, Mesh> columns = new Dictionary<Vector4, Mesh>();
    private static readonly Mesh[] rocks = new Mesh[RockVariants];
    private static Mesh flame;

    // Sizes are rounded to 0.05 so similar sizes share one mesh
    private static float Snap(float value)
    {
        return Mathf.Round(value * 20f) / 20f;
    }

    // ---------- Box ----------

    // uvUnit is how many world units one texture repeat covers
    public static Mesh GetBox(Vector3 size, float uvUnit)
    {
        Vector4 key = new Vector4(Snap(size.x), Snap(size.y), Snap(size.z), uvUnit);

        Mesh mesh;
        if (boxes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        mesh = BuildBox(new Vector3(key.x, key.y, key.z), uvUnit);
        boxes[key] = mesh;
        return mesh;
    }

    private static Mesh BuildBox(Vector3 size, float uvUnit)
    {
        float hx = size.x / 2f;
        float hy = size.y / 2f;
        float hz = size.z / 2f;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        // Top and bottom use (x, z); front and back use (x, y); sides use (z, y)
        AddQuad(verts, normals, uvs, tris,
            new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz),
            Vector3.up, 0, uvUnit);

        AddQuad(verts, normals, uvs, tris,
            new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz),
            Vector3.down, 0, uvUnit);

        AddQuad(verts, normals, uvs, tris,
            new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, -hy, -hz),
            Vector3.back, 1, uvUnit);

        AddQuad(verts, normals, uvs, tris,
            new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, -hy, hz),
            Vector3.forward, 1, uvUnit);

        AddQuad(verts, normals, uvs, tris,
            new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz),
            Vector3.right, 2, uvUnit);

        AddQuad(verts, normals, uvs, tris,
            new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, -hy, -hz),
            Vector3.left, 2, uvUnit);

        return MakeMesh("Box", verts, normals, uvs, tris);
    }

    private static void AddQuad(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, int uvMode, float uvUnit)
    {
        int start = verts.Count;
        Vector3[] corners = { p0, p1, p2, p3 };

        foreach (Vector3 p in corners)
        {
            verts.Add(p);
            normals.Add(normal);

            Vector2 uv;
            if (uvMode == 0) uv = new Vector2(p.x, p.z);
            else if (uvMode == 1) uv = new Vector2(p.x, p.y);
            else uv = new Vector2(p.z, p.y);

            uvs.Add(uv / uvUnit);
        }

        tris.Add(start);
        tris.Add(start + 1);
        tris.Add(start + 2);
        tris.Add(start);
        tris.Add(start + 2);
        tris.Add(start + 3);
    }

    // ---------- Column ----------

    // A round column that stands on y = 0 and has a flat top
    public static Mesh GetColumn(float radius, float height, float uvUnit)
    {
        Vector4 key = new Vector4(Snap(radius), Snap(height), uvUnit, 0f);

        Mesh mesh;
        if (columns.TryGetValue(key, out mesh) && mesh != null) return mesh;

        mesh = BuildColumn(key.x, key.y, uvUnit);
        columns[key] = mesh;
        return mesh;
    }

    private static Mesh BuildColumn(float radius, float height, float uvUnit)
    {
        const int sides = 14;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float circumference = 2f * Mathf.PI * radius;

        // Side: a bottom ring and a top ring, with a repeated seam vertex
        for (int ring = 0; ring < 2; ring++)
        {
            float y = ring * height;

            for (int s = 0; s <= sides; s++)
            {
                float t = (float)s / sides;
                float angle = t * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                verts.Add(direction * radius + new Vector3(0f, y, 0f));
                normals.Add(direction);
                uvs.Add(new Vector2(t * circumference / uvUnit, y / uvUnit));
            }
        }

        int stride = sides + 1;
        for (int s = 0; s < sides; s++)
        {
            int a = s;
            int b = s + 1;
            int c = stride + s;
            int d = stride + s + 1;

            tris.Add(a);
            tris.Add(c);
            tris.Add(b);
            tris.Add(b);
            tris.Add(c);
            tris.Add(d);
        }

        // Flat top
        int centerIndex = verts.Count;
        verts.Add(new Vector3(0f, height, 0f));
        normals.Add(Vector3.up);
        uvs.Add(Vector2.zero);

        int ringStart = verts.Count;
        for (int s = 0; s < sides; s++)
        {
            float angle = (float)s / sides * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);

            verts.Add(p);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(p.x, p.z) / uvUnit);
        }

        for (int s = 0; s < sides; s++)
        {
            int current = ringStart + s;
            int next = ringStart + (s + 1) % sides;

            tris.Add(centerIndex);
            tris.Add(next);
            tris.Add(current);
        }

        return MakeMesh("Column", verts, normals, uvs, tris);
    }

    // ---------- Rock ----------

    // About 1 unit wide with a flat bottom at y = -0.3, so it sits on the ground
    public static Mesh GetRock(int variant)
    {
        variant = Mathf.Abs(variant) % RockVariants;

        if (rocks[variant] == null) rocks[variant] = BuildRock(100 + variant * 31);
        return rocks[variant];
    }

    private static Mesh BuildRock(int seed)
    {
        System.Random rng = new System.Random(seed);

        // Start from an icosahedron (a 20-sided ball)
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
            new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
            new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
        };

        for (int i = 0; i < points.Count; i++)
        {
            points[i] = points[i].normalized;
        }

        int[] baseFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        // Split every triangle into four, for 80 faces
        List<int> faces = new List<int>();
        Dictionary<long, int> midpoints = new Dictionary<long, int>();

        for (int i = 0; i < baseFaces.Length; i += 3)
        {
            int a = baseFaces[i];
            int b = baseFaces[i + 1];
            int c = baseFaces[i + 2];

            int ab = Midpoint(points, midpoints, a, b);
            int bc = Midpoint(points, midpoints, b, c);
            int ca = Midpoint(points, midpoints, c, a);

            faces.AddRange(new int[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        // Push every corner in or out by a random amount, and flatten the bottom
        Vector3[] shape = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            float radius = 0.5f * Mathf.Lerp(0.78f, 1.2f, (float)rng.NextDouble());
            Vector3 p = points[i] * radius;
            p.y = Mathf.Max(p.y, -0.3f);
            shape[i] = p;
        }

        // Give every face its own corners, so each one is shaded flat and looks chiselled
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int i = 0; i < faces.Count; i += 3)
        {
            Vector3 a = shape[faces[i]];
            Vector3 b = shape[faces[i + 1]];
            Vector3 c = shape[faces[i + 2]];

            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;

            // Make sure the face points outward
            if (Vector3.Dot(normal, centroid) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
                normal = -normal;
            }

            normal.Normalize();

            Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            Vector3[] corners = { a, b, c };

            foreach (Vector3 p in corners)
            {
                Vector2 uv;
                if (abs.y >= abs.x && abs.y >= abs.z) uv = new Vector2(p.x, p.z);
                else if (abs.x >= abs.z) uv = new Vector2(p.z, p.y);
                else uv = new Vector2(p.x, p.y);

                tris.Add(verts.Count);
                verts.Add(p);
                normals.Add(normal);
                uvs.Add(uv);
            }
        }

        return MakeMesh("Rock", verts, normals, uvs, tris);
    }

    private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;

        int index;
        if (cache.TryGetValue(key, out index)) return index;

        points.Add(((points[a] + points[b]) * 0.5f).normalized);
        index = points.Count - 1;
        cache[key] = index;
        return index;
    }

    // ---------- Flame ----------

    // A flame tongue that points up: it starts at y = 0, swells quickly, and tapers to a tip at y = 1
    public static Mesh GetFlame()
    {
        if (flame == null) flame = BuildFlame();
        return flame;
    }

    private static Mesh BuildFlame()
    {
        const int rings = 8;
        const int segments = 10;

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float t = (float)r / rings;
            float radius = 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.55f));

            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, t, Mathf.Sin(angle) * radius));
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * segments + s;
                int b = r * segments + (s + 1) % segments;
                int c = (r + 1) * segments + s;
                int d = (r + 1) * segments + (s + 1) % segments;

                tris.Add(a);
                tris.Add(c);
                tris.Add(b);
                tris.Add(b);
                tris.Add(c);
                tris.Add(d);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Flame";
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- Helper ----------

    private static Mesh MakeMesh(string meshName, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
    {
        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}