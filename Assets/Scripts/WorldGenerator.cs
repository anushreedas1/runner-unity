using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Track")]
    [SerializeField] private float segmentLength = 30f;
    [SerializeField] private float trackWidth = 6f;
    [SerializeField] private int segmentsAhead = 6;
    [SerializeField] private float despawnDistanceBehind = 30f;

    [Header("Obstacles")]
    [SerializeField] private float laneDistance = 2f;
    [SerializeField] private int safeSegments = 2;
    [SerializeField] private float[] rowPositions = { 10f, 20f };

    [Header("Coins")]
    [SerializeField] private float[] coinGroupPositions = { 5f, 15f, 25f };
    [SerializeField] private int coinsPerGroup = 5;
    [SerializeField] private float coinSpacing = 1f;
    [SerializeField] private float coinHeight = 1f;

    [Header("Power-ups")]
    [SerializeField] private float powerUpChance = 0.35f;
    [SerializeField] private float powerUpZ = 1f;
    [SerializeField] private float powerUpHeight = 1.2f;

    [Header("Scenery")]
    [SerializeField] private int pinesPerSide = 2;
    [SerializeField] private float ruinChance = 0.7f;
    [SerializeField] private float hillChance = 0.6f;
    [SerializeField] private bool torches = true;

    [Header("Fire blower light")]
    [SerializeField] private bool fireLights = true;
    [SerializeField] private float fireLightIntensity = 3f;

    private readonly Queue<GameObject> segments = new Queue<GameObject>();
    private float nextSpawnZ;
    private int segmentCount;

    // Materials are copied from the player's material, because that one is guaranteed
    // to be included (and to render correctly) in a phone build.
    private Material templateMaterial;
    private readonly Dictionary<Vector4, Material> materialCache = new Dictionary<Vector4, Material>();

    // Path and edges
    private readonly Color groundColorA = new Color(0.22f, 0.22f, 0.24f);
    private readonly Color groundColorB = new Color(0.18f, 0.18f, 0.20f);
    private readonly Color laneLineColor = new Color(0.38f, 0.36f, 0.34f);
    private readonly Color earthColor = new Color(0.07f, 0.10f, 0.08f);
    private readonly Color edgeWallColor = new Color(0.34f, 0.31f, 0.28f);

    // Stone (lighter than the path, so obstacles stand out)
    private readonly Color[] stoneColors =
    {
        new Color(0.62f, 0.55f, 0.46f),
        new Color(0.55f, 0.49f, 0.42f),
        new Color(0.68f, 0.60f, 0.50f)
    };
    private readonly Color darkStoneColor = new Color(0.40f, 0.36f, 0.32f);
    private readonly Color metalColor = new Color(0.10f, 0.10f, 0.11f);

    // Water and fire (values above 1 make them extra bright in the dark)
    private readonly Color waterColor = new Color(0.10f, 0.42f, 0.52f);
    private readonly Color flameOuterColor = new Color(2.6f, 0.7f, 0.1f);
    private readonly Color flameCoreColor = new Color(3f, 2f, 0.5f);

    // Pickups
    private readonly Color coinColor = new Color(1.8f, 1.35f, 0.25f);

    // Forest and mountains
    private readonly Color trunkColor = new Color(0.13f, 0.09f, 0.06f);
    private readonly Color[] pineColors =
    {
        new Color(0.05f, 0.13f, 0.09f),
        new Color(0.07f, 0.17f, 0.11f)
    };
    private readonly Color[] hillColors =
    {
        new Color(0.06f, 0.09f, 0.11f),
        new Color(0.05f, 0.08f, 0.10f)
    };

    void Start()
    {
        nextSpawnZ = -segmentLength;

        if (player != null)
        {
            Renderer playerRenderer = player.GetComponent<Renderer>();
            if (playerRenderer != null) templateMaterial = playerRenderer.sharedMaterial;
        }
    }

    void OnDestroy()
    {
        foreach (Material material in materialCache.Values)
        {
            if (material != null) Destroy(material);
        }
    }

    void Update()
    {
        if (player == null) return;

        // Build track ahead of the player
        while (nextSpawnZ < player.position.z + segmentsAhead * segmentLength)
        {
            SpawnSegment();
        }

        // Remove track far behind the player
        while (segments.Count > 0 &&
               segments.Peek().transform.position.z + segmentLength < player.position.z - despawnDistanceBehind)
        {
            Destroy(segments.Dequeue());
        }
    }

    private void SpawnSegment()
    {
        GameObject segment = new GameObject("Segment " + segmentCount);
        segment.transform.SetParent(transform);
        segment.transform.position = new Vector3(0f, 0f, nextSpawnZ);
        Transform root = segment.transform;

        // Path: two slabs per section, alternating shades so you can see the speed
        float half = segmentLength / 2f;
        for (int i = 0; i < 2; i++)
        {
            Color color = (i == 0) ? groundColorA : groundColorB;
            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(0f, -0.5f, half * (i + 0.5f)),
                new Vector3(trackWidth, 1f, half),
                Quaternion.identity, color, 0.1f, true);
        }

        // Earth, edge walls, lane lines, ruins, pines, hills and torches
        SpawnScenery(root);

        // Obstacles (skip the first few segments so the player can get going)
        if (segmentCount >= safeSegments)
        {
            foreach (float z in rowPositions)
            {
                SpawnObstacleRow(root, z);
            }
        }

        // Coins (segment 0 is behind the start line, so skip it)
        if (segmentCount >= 1)
        {
            foreach (float z in coinGroupPositions)
            {
                SpawnCoinGroup(root, z);
            }
        }

        // Power-ups
        if (segmentCount >= safeSegments && Random.value < powerUpChance)
        {
            SpawnPowerUp(root);
        }

        segments.Enqueue(segment);
        nextSpawnZ += segmentLength;
        segmentCount++;
    }

    // ---------- Scenery ----------

    private void SpawnScenery(Transform parent)
    {
        float halfTrack = trackWidth / 2f;
        float earthWidth = 70f;

        for (int side = -1; side <= 1; side += 2)
        {
            // Dark earth beside the path (a little lower than the path)
            CreatePiece(parent, PrimitiveType.Cube,
                new Vector3(side * (halfTrack + earthWidth / 2f), -0.6f, segmentLength / 2f),
                new Vector3(earthWidth, 1f, segmentLength),
                Quaternion.identity, earthColor, 0.05f, false);

            // Low stone wall along the path edge
            CreatePiece(parent, PrimitiveType.Cube,
                new Vector3(side * (halfTrack + 0.3f), 0.275f, segmentLength / 2f),
                new Vector3(0.6f, 0.55f, segmentLength),
                Quaternion.identity, edgeWallColor, 0.1f, false);

            // Worn lane line
            CreatePiece(parent, PrimitiveType.Cube,
                new Vector3(side * laneDistance / 2f, 0.011f, segmentLength / 2f),
                new Vector3(0.08f, 0.02f, segmentLength),
                Quaternion.identity, laneLineColor, 0.1f, false);

            // Broken column
            if (Random.value < ruinChance) SpawnRuin(parent, side, halfTrack);

            // Tall dark pines
            for (int i = 0; i < pinesPerSide; i++)
            {
                Vector3 basePosition = new Vector3(
                    side * (halfTrack + Random.Range(6f, 22f)),
                    -0.1f,
                    Random.Range(1f, segmentLength - 1f));

                SpawnPine(parent, basePosition, Random.Range(0.8f, 1.5f));
            }

            // Distant mountains that fade into the mist
            if (Random.value < hillChance)
            {
                float diameter = Random.Range(45f, 90f);
                float topHeight = Random.Range(14f, 40f);

                CreatePiece(parent, PrimitiveType.Sphere,
                    new Vector3(side * (halfTrack + Random.Range(50f, 95f)), topHeight - diameter / 2f, Random.Range(0f, segmentLength)),
                    Vector3.one * diameter,
                    Quaternion.identity, hillColors[Random.Range(0, hillColors.Length)], 0.05f, false);
            }
        }

        // One torch per section, alternating sides
        if (torches) SpawnTorch(parent, (segmentCount % 2 == 0) ? -1 : 1, halfTrack);
    }

    private void SpawnRuin(Transform parent, int side, float halfTrack)
    {
        float x = side * (halfTrack + Random.Range(1.8f, 5f));
        float z = Random.Range(2f, segmentLength - 2f);
        float height = Random.Range(1.5f, 5f);
        Color stone = darkStoneColor;

        // Square base, then the column (a cylinder is 2 units tall at scale 1)
        CreatePiece(parent, PrimitiveType.Cube,
            new Vector3(x, 0.15f, z), new Vector3(1.5f, 0.4f, 1.5f),
            Quaternion.identity, darkStoneColor, 0.1f, false);

        CreatePiece(parent, PrimitiveType.Cylinder,
            new Vector3(x, 0.3f + height / 2f, z), new Vector3(0.95f, height / 2f, 0.95f),
            Quaternion.identity, stone, 0.1f, false);

        // Tall columns still have their top slab
        if (height > 3.2f)
        {
            CreatePiece(parent, PrimitiveType.Cube,
                new Vector3(x, 0.3f + height + 0.12f, z), new Vector3(1.4f, 0.25f, 1.4f),
                Quaternion.Euler(0f, Random.Range(0f, 45f), 0f), darkStoneColor, 0.1f, false);
        }

        // Sometimes a fallen column lies beside it
        if (Random.value < 0.35f)
        {
            CreatePiece(parent, PrimitiveType.Cylinder,
                new Vector3(x + side * Random.Range(1.5f, 3f), 0.45f, z + Random.Range(-3f, 3f)),
                new Vector3(0.9f, 1.3f, 0.9f),
                Quaternion.Euler(0f, Random.Range(0f, 180f), 90f), stone, 0.1f, false);
        }
    }

    private void SpawnPine(Transform parent, Vector3 basePosition, float size)
    {
        float height = Random.Range(7f, 12f) * size;
        Color leaf = pineColors[Random.Range(0, pineColors.Length)];

        // Thin trunk
        CreatePiece(parent, PrimitiveType.Cylinder,
            basePosition + new Vector3(0f, height / 2f, 0f),
            new Vector3(0.5f * size, height / 2f, 0.5f * size),
            Quaternion.identity, trunkColor, 0.05f, false);

        // Two tall, narrow clumps of needles
        CreatePiece(parent, PrimitiveType.Sphere,
            basePosition + new Vector3(0f, height * 0.62f, 0f),
            new Vector3(3.4f * size, height * 0.55f, 3.4f * size),
            Quaternion.identity, leaf, 0.05f, false);

        CreatePiece(parent, PrimitiveType.Sphere,
            basePosition + new Vector3(0f, height * 0.88f, 0f),
            new Vector3(2.2f * size, height * 0.35f, 2.2f * size),
            Quaternion.identity, leaf, 0.05f, false);
    }

    private void SpawnTorch(Transform parent, int side, float halfTrack)
    {
        GameObject torch = new GameObject("Torch");
        torch.transform.SetParent(parent, false);
        torch.transform.localPosition = new Vector3(side * (halfTrack + 1.3f), 0f, segmentLength * 0.5f);
        Transform t = torch.transform;

        // Stone post and bowl
        CreatePiece(t, PrimitiveType.Cylinder,
            new Vector3(0f, 0.9f, 0f), new Vector3(0.35f, 0.9f, 0.35f),
            Quaternion.identity, darkStoneColor, 0.1f, false);

        CreatePiece(t, PrimitiveType.Cube,
            new Vector3(0f, 1.85f, 0f), new Vector3(0.7f, 0.2f, 0.7f),
            Quaternion.identity, darkStoneColor, 0.1f, false);

        // Flame
        GameObject flame = new GameObject("Flames");
        flame.transform.SetParent(t, false);
        flame.transform.localPosition = new Vector3(0f, 2.3f, 0f);

        CreatePiece(flame.transform, PrimitiveType.Sphere,
            Vector3.zero, new Vector3(0.6f, 0.95f, 0.6f),
            Quaternion.identity, flameOuterColor, 0f, false);

        CreatePiece(flame.transform, PrimitiveType.Sphere,
            new Vector3(0f, -0.05f, 0f), new Vector3(0.35f, 0.6f, 0.35f),
            Quaternion.identity, flameCoreColor, 0f, false);

        flame.AddComponent<FlameFlicker>();
    }

    // ---------- Obstacles ----------

    private void SpawnObstacleRow(Transform parent, float z)
    {
        switch (Random.Range(0, 5))
        {
            case 0:
                SpawnBoulder(parent, LaneX(Random.Range(0, 3)), z);
                break;
            case 1:
                SpawnWaterChannel(parent, z);
                break;
            case 2:
                SpawnFireBlower(parent, z);
                break;
            case 3:
                SpawnPillars(parent, z);
                break;
            default:
                SpawnLowWall(parent, z);
                break;
        }
    }

    // A hidden trigger box that ends the run (or stumbles you) when you touch it.
    // The visible pieces are children of this object and have no colliders of their own.
    private Transform CreateObstacleRoot(Transform parent, string objectName, Vector3 localPosition,
        Vector3 colliderCenter, Vector3 colliderSize)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = colliderCenter;
        box.size = colliderSize;
        box.isTrigger = true;

        root.AddComponent<Obstacle>();
        return root.transform;
    }

    // Boulder in one lane: jump over it or switch lane
    private void SpawnBoulder(Transform parent, float x, float z)
    {
        Transform root = CreateObstacleRoot(parent, "Boulder", new Vector3(x, 0f, z),
            new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 1f, 1.4f));

        CreatePiece(root, PrimitiveType.Sphere,
            new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 1.15f, 1.4f),
            Quaternion.Euler(0f, Random.Range(0f, 180f), 0f), RandomStone(), 0.1f, true);

        CreatePiece(root, PrimitiveType.Cube,
            new Vector3(0.55f, 0.28f, -0.45f), new Vector3(0.7f, 0.55f, 0.6f),
            Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 90f), Random.Range(-15f, 15f)),
            RandomStone(), 0.1f, true);

        CreatePiece(root, PrimitiveType.Sphere,
            new Vector3(-0.6f, 0.22f, 0.4f), new Vector3(0.6f, 0.45f, 0.55f),
            Quaternion.Euler(0f, Random.Range(0f, 180f), 0f), RandomStone(), 0.1f, true);
    }

    // Water across the whole path: jump over it
    private void SpawnWaterChannel(Transform parent, float z)
    {
        float length = 3.2f;

        Transform root = CreateObstacleRoot(parent, "WaterChannel", new Vector3(0f, 0f, z),
            new Vector3(0f, 0.1f, 0f), new Vector3(trackWidth, 0.6f, length + 0.4f));

        // Glossy water surface
        CreatePiece(root, PrimitiveType.Cube,
            new Vector3(0f, 0.02f, 0f), new Vector3(trackWidth, 0.04f, length),
            Quaternion.identity, waterColor, 0.95f, false);

        // Stone lips at both ends
        for (int end = -1; end <= 1; end += 2)
        {
            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(0f, 0.1f, end * length / 2f), new Vector3(trackWidth, 0.2f, 0.3f),
                Quaternion.identity, darkStoneColor, 0.1f, true);
        }
    }

    // Flame band across the path at head height: slide under it
    private void SpawnFireBlower(Transform parent, float z)
    {
        float bandY = 1.65f;
        float postX = trackWidth / 2f - 0.25f;

        Transform root = CreateObstacleRoot(parent, "FireBlower", new Vector3(0f, 0f, z),
            new Vector3(0f, bandY, 0f), new Vector3(trackWidth, 1f, 0.8f));

        for (int side = -1; side <= 1; side += 2)
        {
            // Stone post
            CreatePiece(root, PrimitiveType.Cylinder,
                new Vector3(side * postX, 1.4f, 0f), new Vector3(0.7f, 1.4f, 0.7f),
                Quaternion.identity, darkStoneColor, 0.1f, true);

            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(side * postX, 2.85f, 0f), new Vector3(0.95f, 0.2f, 0.95f),
                Quaternion.identity, RandomStone(), 0.1f, true);

            // Iron nozzle pointing across the path
            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(side * (postX - 0.5f), bandY, 0f), new Vector3(0.5f, 0.35f, 0.35f),
                Quaternion.identity, metalColor, 0.3f, true);
        }

        // The flames
        GameObject flames = new GameObject("Flames");
        flames.transform.SetParent(root, false);
        flames.transform.localPosition = new Vector3(0f, bandY, 0f);

        float[] puffX = { -2.1f, -0.7f, 0.7f, 2.1f };
        foreach (float x in puffX)
        {
            CreatePiece(flames.transform, PrimitiveType.Sphere,
                new Vector3(x, 0f, 0f), new Vector3(1.9f, 1.0f, 0.75f),
                Quaternion.identity, flameOuterColor, 0f, false);

            CreatePiece(flames.transform, PrimitiveType.Sphere,
                new Vector3(x, 0f, 0f), new Vector3(1.3f, 0.6f, 0.5f),
                Quaternion.identity, flameCoreColor, 0f, false);
        }

        flames.AddComponent<FlameFlicker>();

        // Orange glow on the stones around it
        if (fireLights)
        {
            GameObject lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, bandY - 0.3f, -1.5f);

            Light glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.2f);
            glow.intensity = fireLightIntensity;
            glow.range = 11f;
            glow.shadows = LightShadows.None;
        }
    }

    // Stacked stone pillars in two lanes: move to the free lane
    private void SpawnPillars(Transform parent, float z)
    {
        int freeLane = Random.Range(0, 3);

        for (int lane = 0; lane < 3; lane++)
        {
            if (lane == freeLane) continue;

            Transform root = CreateObstacleRoot(parent, "StonePillar", new Vector3(LaneX(lane), 0f, z),
                new Vector3(0f, 1.4f, 0f), new Vector3(1.6f, 2.8f, 1.4f));

            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 1.1f, 1.4f),
                Yaw(4f), darkStoneColor, 0.1f, true);

            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(0f, 1.5f, 0f), new Vector3(1.45f, 0.95f, 1.3f),
                Yaw(4f), RandomStone(), 0.1f, true);

            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(0f, 2.35f, 0f), new Vector3(1.3f, 0.9f, 1.2f),
                Yaw(4f), RandomStone(), 0.1f, true);

            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(0f, 2.9f, 0f), new Vector3(1.7f, 0.2f, 1.5f),
                Yaw(3f), darkStoneColor, 0.1f, true);
        }
    }

    // Broken low wall across the whole path: jump over it
    private void SpawnLowWall(Transform parent, float z)
    {
        Transform root = CreateObstacleRoot(parent, "RuinedWall", new Vector3(0f, 0f, z),
            new Vector3(0f, 0.5f, 0f), new Vector3(trackWidth, 1f, 0.9f));

        for (int lane = 0; lane < 3; lane++)
        {
            float height = Random.Range(0.65f, 1f);

            CreatePiece(root, PrimitiveType.Cube,
                new Vector3(LaneX(lane), height / 2f, 0f), new Vector3(laneDistance - 0.05f, height, 0.9f),
                Yaw(2f), RandomStone(), 0.1f, true);
        }
    }

    // ---------- Coins and power-ups ----------

    private void SpawnCoinGroup(Transform parent, float centerZ)
    {
        float x = LaneX(Random.Range(0, 3));

        for (int i = 0; i < coinsPerGroup; i++)
        {
            float z = centerZ + (i - (coinsPerGroup - 1) / 2f) * coinSpacing;
            CreateCoin(parent, new Vector3(x, coinHeight, z));
        }
    }

    private void CreateCoin(Transform parent, Vector3 localPosition)
    {
        GameObject coin = new GameObject("Coin");
        coin.transform.SetParent(parent, false);
        coin.transform.localPosition = localPosition;

        SphereCollider trigger = coin.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;

        coin.AddComponent<Coin>();

        // Flat gold disc, turned so it spins like a real coin
        CreatePiece(coin.transform, PrimitiveType.Cylinder,
            Vector3.zero, new Vector3(0.7f, 0.06f, 0.7f),
            Quaternion.Euler(0f, 0f, 90f), coinColor, 0.6f, true);
    }

    private void SpawnPowerUp(Transform parent)
    {
        PowerUpType type = (PowerUpType)Random.Range(0, 3);
        Vector3 localPosition = new Vector3(LaneX(Random.Range(0, 3)), powerUpHeight, powerUpZ);

        PrimitiveType shape;
        Color color;
        Vector3 scale;

        switch (type)
        {
            case PowerUpType.Magnet:
                shape = PrimitiveType.Cube;
                color = new Color(1.8f, 0.3f, 0.3f);
                scale = Vector3.one * 0.8f;
                break;
            case PowerUpType.Shield:
                shape = PrimitiveType.Sphere;
                color = new Color(0.3f, 1.6f, 2f);
                scale = Vector3.one * 0.9f;
                break;
            default:
                shape = PrimitiveType.Capsule;
                color = new Color(0.5f, 2f, 0.5f);
                scale = new Vector3(0.6f, 0.5f, 0.6f);
                break;
        }

        GameObject root = new GameObject("PowerUp " + type);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.9f;

        PowerUp powerUp = root.AddComponent<PowerUp>();
        powerUp.Type = type;

        CreatePiece(root.transform, shape, Vector3.zero, scale, Quaternion.identity, color, 0.5f, true);
    }

    // ---------- Helpers ----------

    private float LaneX(int lane)
    {
        return (lane - 1) * laneDistance;
    }

    private Color RandomStone()
    {
        return stoneColors[Random.Range(0, stoneColors.Length)];
    }

    private static Quaternion Yaw(float range)
    {
        return Quaternion.Euler(0f, Random.Range(-range, range), 0f);
    }

    // Builds one visible piece. "detailed" pieces cast and receive shadows;
    // scenery skips shadows to keep phones running smoothly. Pieces never have colliders.
    private GameObject CreatePiece(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale,
        Quaternion rotation, Color color, float smoothness, bool detailed)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = scale;
        Paint(piece, color, smoothness);

        if (!detailed)
        {
            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            pieceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pieceRenderer.receiveShadows = false;
        }

        Destroy(piece.GetComponent<Collider>());
        return piece;
    }

    private Material GetMaterial(Color color, float smoothness)
    {
        Vector4 key = new Vector4(color.r, color.g, color.b, smoothness);

        Material material;
        if (materialCache.TryGetValue(key, out material)) return material;

        if (templateMaterial == null) return null;

        material = new Material(templateMaterial);
        material.color = color;
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_Metallic", 0f);

        materialCache[key] = material;
        return material;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private void Paint(GameObject target, Color color, float smoothness)
    {
        Renderer targetRenderer = target.GetComponent<Renderer>();
        Material material = GetMaterial(color, smoothness);

        if (material != null) targetRenderer.sharedMaterial = material;
        else targetRenderer.material.color = color;
    }
}