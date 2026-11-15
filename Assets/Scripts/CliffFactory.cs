using System.Collections.Generic;
using UnityEngine;

// Builds craggy cliff shapes: a 1,280-face ball pushed in and out by noise, shaded flat
// so every face looks chiselled. The world stretches them tall.
public static class CliffFactory
{
    public const int Variants = 4;

    private static readonly Mesh[] cliffs = new Mesh[Variants];

    public static Mesh Get(int variant)
    {
        variant = Mathf.Abs(variant) % Variants;

        if (cliffs[variant] == null) cliffs[variant] = Build(500 + variant * 57);
        return cliffs[variant];
    }

    private static Mesh Build(int seed)
    {
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

        List<int> faces = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        // Split every triangle into four, three times over: 20 faces become 1,280
        for (int pass = 0; pass < 3; pass++)
        {
            Dictionary<long, int> midpoints = new Dictionary<long, int>();
            List<int> next = new List<int>();

            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i];
                int b = faces[i + 1];
                int c = faces[i + 2];

                int ab = Midpoint(points, midpoints, a, b);
                int bc = Midpoint(points, midpoints, b, c);
                int ca = Midpoint(points, midpoints, c, a);

                next.AddRange(new int[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            faces = next;
        }

        // Push every corner in or out with noise to make cliffs and crags
        float offset = 20f + seed * 0.13f;
        Vector3[] shape = new Vector3[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 direction = points[i];

            float bulk = Fbm(direction * 1.8f, offset);
            float crag = Mathf.Abs(Fbm(direction * 4f, offset + 9f) - 0.5f) * 2f;

            float radius = 0.5f * (0.55f + 0.7f * bulk + 0.25f * crag);
            shape[i] = direction * radius;
        }

        // Give every face its own corners, so each one is shaded flat
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

        Mesh mesh = new Mesh();
        mesh.name = "Cliff";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
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

    // Three layers of noise, each finer than the last
    private static float Fbm(Vector3 p, float offset)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;
        float frequency = 1f;

        for (int i = 0; i < 3; i++)
        {
            sum += Noise3(p * frequency, offset + i * 13f) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return sum / total;
    }

    // 3D noise made by mixing three flat Perlin noises
    private static float Noise3(Vector3 p, float offset)
    {
        float xy = Mathf.PerlinNoise(p.x + offset, p.y + offset * 0.7f);
        float yz = Mathf.PerlinNoise(p.y + offset * 1.3f, p.z + offset);
        float zx = Mathf.PerlinNoise(p.z + offset * 0.9f, p.x + offset * 1.1f);
        return (xy + yz + zx) / 3f;
    }
}