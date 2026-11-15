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
    [SerializeField] private float cliffChance = 0.7f;
    [SerializeField] private float plankChance = 0.3f;
    [SerializeField] private bool gates = true;
    [SerializeField] private bool torches = true;

    [Header("Fire")]
    [SerializeField] private Material fireParticleMaterial;
    [SerializeField] private bool fireLights = true;
    [SerializeField] private float fireLightIntensity = 3f;

    // Size of one texture repeat, in world units
    private const float PathUV = 6f;
    private const float BlockUV = 2.5f;
    private const float WoodUV = 6f;
    private const float WaterUV = 4f;

    private readonly Queue<GameObject> segments = new Queue<GameObject>();
    private float nextSpawnZ;
    private int segmentCount;

    // Materials are copied from the player's material, because that one is guaranteed
    // to be included (and to render correctly) in a phone build.
    private Material templateMaterial;
    private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    private Material pathStoneMaterial;
    private Material pathPlankMaterial;
    private Material wallMaterial;
    private Material sandstoneMaterial;
    private Material darkStoneMaterial;
    private Material rockMaterial;
    private Material cliffMaterial;
    private Material waterMaterial;
    private Material laneLineMaterial;
    private Material metalMaterial;
    private Material flameOuterMaterial;
    private Material flameMidMaterial;
    private Material flameCoreMaterial;
    private Material coinMaterial;

    // Particle fire (null until the FireParticles material is assigned)
    private Material fireMaterial;

    void Start()
    {
        nextSpawnZ = -segmentLength;

        if (player != null)
        {
            Renderer playerRenderer = player.GetComponent<Renderer>();
            if (playerRenderer != null) templateMaterial = playerRenderer.sharedMaterial;
        }

        // Paint the textures and build the shapes now, so nothing hitches mid-run
        TextureFactory.PreloadAll();

        for (int i = 0; i < MeshFactory.RockVariants; i++)
        {
            MeshFactory.GetRock(i);
        }

        for (int i = 0; i < CliffFactory.Variants; i++)
        {
            CliffFactory.Get(i);
        }

        MeshFactory.GetFlame();

        if (fireParticleMaterial != null)
        {
            fireMaterial = new Material(fireParticleMaterial);
            fireMaterial.mainTexture = FireEffect.GetTexture();
        }

        BuildMaterials();
    }

    void OnDestroy()
    {
        foreach (Material material in materialCache.Values)
        {
            if (material != null) Destroy(material);
        }

        if (fireMaterial != null) Destroy(fireMaterial);
    }

    private void BuildMaterials()
    {
        pathStoneMaterial = Textured(TextureFactory.Kind.StoneSlabs, new Color(0.62f, 0.62f, 0.68f), 0.12f, 1f);
        pathPlankMaterial = Textured(TextureFactory.Kind.Planks, new Color(0.72f, 0.66f, 0.62f), 0.1f, 1f);
        wallMaterial = Textured(TextureFactory.Kind.StoneBlocks, new Color(0.62f, 0.58f, 0.56f), 0.1f, 1f);
        sandstoneMaterial = Textured(TextureFactory.Kind.StoneBlocks, new Color(1.0f, 0.93f, 0.82f), 0.1f, 1f);
        darkStoneMaterial = Textured(TextureFactory.Kind.StoneBlocks, new Color(0.68f, 0.64f, 0.60f), 0.1f, 1f);
        rockMaterial = Textured(TextureFactory.Kind.Rock, new Color(1f, 0.96f, 0.90f), 0.1f, 1f);
        cliffMaterial = Textured(TextureFactory.Kind.Cliff, new Color(0.45f, 0.40f, 0.40f), 0.05f, 3f);
        waterMaterial = Textured(TextureFactory.Kind.Water, Color.white, 0.95f, 1f);

        laneLineMaterial = Flat(new Color(0.55f, 0.50f, 0.44f), 0.1f);
        metalMaterial = Flat(new Color(0.10f, 0.10f, 0.11f), 0.4f);

        // Values above 1 make the flames extra bright in the dark (used if there is no particle fire)
        flameOuterMaterial = Flat(new Color(2.4f, 0.55f, 0.08f), 0f);
        flameMidMaterial = Flat(new Color(3f, 1.3f, 0.2f), 0f);
        flameCoreMaterial = Flat(new Color(3f, 2.4f, 0.8f), 0f);

        coinMaterial = Flat(new Color(1.8f, 1.35f, 0.25f), 0.6f);
    }

    void Update()
    {
        if (player == null) return;

        // Make the water ripple
        if (waterMaterial != null)
        {
            waterMaterial.mainTextureOffset = new Vector2(Time.time * 0.03f, Time.time * 0.12f);
        }

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

        // Path: carved stone slabs, or sometimes a wooden plank bridge
        bool plank = segmentCount >= safeSegments && Random.value < plankChance;
        Box(root, new Vector3(trackWidth, 1f, segmentLength),
            new Vector3(0f, -0.5f, segmentLength / 2f), Quaternion.identity,
            plank ? pathPlankMaterial : pathStoneMaterial, plank ? WoodUV : PathUV, true);

        // Walls, posts, torches, rock pillars and gates
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

        for (int side = -1; side <= 1; side += 2)
        {
            // Carved stone wall along the edge
            Box(parent, new Vector3(0.6f, 0.6f, segmentLength),
                new Vector3(side * (halfTrack + 0.3f), 0.3f, segmentLength / 2f),
                Quaternion.identity, wallMaterial, BlockUV, false);

            // Worn lane line
            Box(parent, new Vector3(0.08f, 0.02f, segmentLength),
                new Vector3(side * laneDistance / 2f, 0.011f, segmentLength / 2f),
                Quaternion.identity, laneLineMaterial, 1f, false);

            // Stone posts, with a torch on one of them (alternating sides)
            bool torchSide = side == ((segmentCount % 2 == 0) ? -1 : 1);
            SpawnPost(parent, side, segmentLength * 0.25f, false);
            SpawnPost(parent, side, segmentLength * 0.75f, torches && torchSide);

            // Rock pillars rising out of the mist
            SpawnCliffs(parent, side, halfTrack);
        }

        // A giant stone archway every few sections
        if (gates && segmentCount % 4 == 3) SpawnGate(parent, halfTrack);
    }

    private void SpawnPost(Transform parent, int side, float z, bool withTorch)
    {
        float x = side * (trackWidth / 2f + 0.3f);
        float height = Mathf.Round(Random.Range(1.4f, 2.4f) * 4f) / 4f;

        Box(parent, new Vector3(0.9f, height, 0.9f),
            new Vector3(x, height / 2f, z),
            Quaternion.identity, darkStoneMaterial, BlockUV, false);

        Box(parent, new Vector3(1.1f, 0.2f, 1.1f),
            new Vector3(x, height + 0.1f, z),
            Yaw(8f), wallMaterial, BlockUV, false);

        if (withTorch) SpawnTorch(parent, new Vector3(x, height + 0.2f, z));
    }

    private void SpawnTorch(Transform parent, Vector3 position)
    {
        GameObject torch = new GameObject("Torch");
        torch.transform.SetParent(parent, false);
        torch.transform.localPosition = position;
        Transform t = torch.transform;

        // Iron bowl
        Box(t, new Vector3(0.6f, 0.18f, 0.6f), new Vector3(0f, 0.09f, 0f),
            Quaternion.identity, metalMaterial, 1f, false);

        if (fireMaterial != null)
        {
            // Real particle fire, shooting up
            FireEffect.Create(t, new Vector3(0f, 0.18f, 0f), Quaternion.Euler(-90f, 0f, 0f), fireMaterial,
                1.2f, 0.7f, 0.6f, 28f, 8f, 0.12f);
            return;
        }

        // No particle material yet: use the flame shapes
        GameObject flame = new GameObject("Flames");
        flame.transform.SetParent(t, false);
        flame.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        Mesh flameMesh = MeshFactory.GetFlame();
        PlaceMesh(flame.transform, flameMesh, Vector3.zero, new Vector3(0.5f, 1.0f, 0.5f),
            Quaternion.identity, flameOuterMaterial, false);
        PlaceMesh(flame.transform, flameMesh, Vector3.zero, new Vector3(0.28f, 0.65f, 0.28f),
            Quaternion.identity, flameCoreMaterial, false);

        flame.AddComponent<FlameFlicker>();
    }

    private void SpawnCliffs(Transform parent, int side, float halfTrack)
    {
        // Close rock pillars that disappear down into the mist
        if (Random.value < 0.8f)
        {
            float width = Random.Range(4f, 8f);
            float tall = Random.Range(40f, 70f);
            float top = Random.Range(-2f, 12f);

            PlaceMesh(parent, CliffFactory.Get(Random.Range(0, CliffFactory.Variants)),
                new Vector3(side * (halfTrack + Random.Range(10f, 35f)), top - tall * 0.55f, Random.Range(0f, segmentLength)),
                new Vector3(width, tall, width * Random.Range(0.8f, 1.3f)),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), cliffMaterial, false);
        }

        // Far mountains
        if (Random.value < cliffChance)
        {
            float width = Random.Range(14f, 26f);
            float tall = Random.Range(60f, 100f);
            float top = Random.Range(10f, 34f);

            PlaceMesh(parent, CliffFactory.Get(Random.Range(0, CliffFactory.Variants)),
                new Vector3(side * (halfTrack + Random.Range(40f, 80f)), top - tall * 0.55f, Random.Range(0f, segmentLength)),
                new Vector3(width, tall, width * Random.Range(0.8f, 1.3f)),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), cliffMaterial, false);
        }
    }

    // Two huge pillars and a lintel. The camera passes safely underneath.
    private void SpawnGate(Transform parent, float halfTrack)
    {
        float z = segmentLength * 0.5f;
        float x = halfTrack + 1.4f;

        for (int side = -1; side <= 1; side += 2)
        {
            Box(parent, new Vector3(1.8f, 10f, 1.8f), new Vector3(side * x, 5f, z),
                Quaternion.identity, darkStoneMaterial, BlockUV, false);

            Box(parent, new Vector3(2.3f, 0.5f, 2.3f), new Vector3(side * x, 10.25f, z),
                Quaternion.identity, wallMaterial, BlockUV, false);
        }

        Box(parent, new Vector3(2f * x + 1.8f, 1.2f, 1.8f), new Vector3(0f, 9.2f, z),
            Quaternion.identity, darkStoneMaterial, BlockUV, false);
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

        PlaceMesh(root, RandomRock(), new Vector3(0f, 0.375f, 0f), new Vector3(1.8f, 1.25f, 1.5f),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), rockMaterial, true);

        PlaceMesh(root, RandomRock(), new Vector3(0.7f, 0.18f, -0.5f), new Vector3(0.8f, 0.6f, 0.7f),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), rockMaterial, true);

        PlaceMesh(root, RandomRock(), new Vector3(-0.75f, 0.15f, 0.45f), new Vector3(0.6f, 0.5f, 0.6f),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), rockMaterial, true);
    }

    // Water across the whole path: jump over it
    private void SpawnWaterChannel(Transform parent, float z)
    {
        float length = 3.2f;

        Transform root = CreateObstacleRoot(parent, "WaterChannel", new Vector3(0f, 0f, z),
            new Vector3(0f, 0.1f, 0f), new Vector3(trackWidth, 0.6f, length + 0.4f));

        // Rippling water surface
        Box(root, new Vector3(trackWidth, 0.04f, length), new Vector3(0f, 0.02f, 0f),
            Quaternion.identity, waterMaterial, WaterUV, false);

        // Stone lips at both ends
        for (int end = -1; end <= 1; end += 2)
        {
            Box(root, new Vector3(trackWidth, 0.2f, 0.3f), new Vector3(0f, 0.1f, end * length / 2f),
                Quaternion.identity, darkStoneMaterial, BlockUV, true);
        }
    }

    // Jets of flame across the path at head height: slide under them
    private void SpawnFireBlower(Transform parent, float z)
    {
        float bandY = 1.65f;
        float postX = trackWidth / 2f - 0.25f;

        Transform root = CreateObstacleRoot(parent, "FireBlower", new Vector3(0f, 0f, z),
            new Vector3(0f, bandY, 0f), new Vector3(trackWidth, 1f, 0.8f));

        for (int side = -1; side <= 1; side += 2)
        {
            // Stone column with a cap
            Column(root, 0.38f, 2.8f, new Vector3(side * postX, 0f, 0f), darkStoneMaterial, BlockUV, true);

            Box(root, new Vector3(1f, 0.2f, 1f), new Vector3(side * postX, 2.9f, 0f),
                Yaw(6f), sandstoneMaterial, BlockUV, true);

            // Iron nozzle pointing across the path
            Box(root, new Vector3(0.5f, 0.35f, 0.35f), new Vector3(side * (postX - 0.5f), bandY, 0f),
                Quaternion.identity, metalMaterial, 1f, true);
        }

        if (fireMaterial != null)
        {
            // Two jets of real fire that meet in the middle
            for (int side = -1; side <= 1; side += 2)
            {
                float nozzleX = side * (postX - 0.7f);
                Quaternion aim = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);

                FireEffect.Create(root, new Vector3(nozzleX, bandY, 0f), aim, fireMaterial,
                    3.2f, 0.85f, 1.0f, 45f, 5f, 0.18f);
            }

            // Makes the glow flicker
            GameObject flicker = new GameObject("LightFlicker");
            flicker.transform.SetParent(root, false);
            flicker.AddComponent<FlameFlicker>();
        }
        else
        {
            // No particle material yet: use the flame shapes
            GameObject flames = new GameObject("Flames");
            flames.transform.SetParent(root, false);
            flames.transform.localPosition = new Vector3(0f, bandY, 0f);

            Mesh flameMesh = MeshFactory.GetFlame();
            for (int side = -1; side <= 1; side += 2)
            {
                float nozzleX = side * (postX - 0.7f);

                // A flame is built pointing up, so tip it over to point across the path
                Quaternion tilt = Quaternion.Euler(0f, 0f, side < 0 ? -90f : 90f);

                PlaceMesh(flames.transform, flameMesh, new Vector3(nozzleX, 0f, 0f),
                    new Vector3(0.95f, 2.6f, 0.95f), tilt, flameOuterMaterial, false);
                PlaceMesh(flames.transform, flameMesh, new Vector3(nozzleX, 0f, 0f),
                    new Vector3(0.65f, 2.2f, 0.65f), tilt, flameMidMaterial, false);
                PlaceMesh(flames.transform, flameMesh, new Vector3(nozzleX, 0f, 0f),
                    new Vector3(0.35f, 1.7f, 0.35f), tilt, flameCoreMaterial, false);
            }

            flames.AddComponent<FlameFlicker>();
        }

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

            Box(root, new Vector3(1.6f, 1.1f, 1.4f), new Vector3(0f, 0.55f, 0f),
                Yaw(4f), darkStoneMaterial, BlockUV, true);

            Box(root, new Vector3(1.45f, 0.95f, 1.3f), new Vector3(0f, 1.5f, 0f),
                Yaw(4f), sandstoneMaterial, BlockUV, true);

            Box(root, new Vector3(1.3f, 0.9f, 1.2f), new Vector3(0f, 2.35f, 0f),
                Yaw(4f), sandstoneMaterial, BlockUV, true);

            Box(root, new Vector3(1.7f, 0.2f, 1.5f), new Vector3(0f, 2.9f, 0f),
                Yaw(3f), darkStoneMaterial, BlockUV, true);
        }
    }

    // Broken low wall across the whole path: jump over it
    private void SpawnLowWall(Transform parent, float z)
    {
        Transform root = CreateObstacleRoot(parent, "RuinedWall", new Vector3(0f, 0f, z),
            new Vector3(0f, 0.5f, 0f), new Vector3(trackWidth, 1f, 0.9f));

        for (int lane = 0; lane < 3; lane++)
        {
            float height = Mathf.Round(Random.Range(0.65f, 1f) * 10f) / 10f;

            Box(root, new Vector3(laneDistance - 0.05f, height, 0.9f),
                new Vector3(LaneX(lane), height / 2f, 0f),
                Yaw(2f), sandstoneMaterial, BlockUV, true);

            // Fallen rubble on some of the blocks
            if (Random.value < 0.6f)
            {
                PlaceMesh(root, RandomRock(),
                    new Vector3(LaneX(lane) + Random.Range(-0.5f, 0.5f), height + 0.08f, Random.Range(-0.2f, 0.2f)),
                    new Vector3(0.55f, 0.35f, 0.45f),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), rockMaterial, true);
            }
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
        Primitive(coin.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.7f, 0.06f, 0.7f),
            Quaternion.Euler(0f, 0f, 90f), coinMaterial, true);
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

        Primitive(root.transform, shape, Vector3.zero, scale, Quaternion.identity, Flat(color, 0.5f), true);
    }

    // ---------- Helpers ----------

    private float LaneX(int lane)
    {
        return (lane - 1) * laneDistance;
    }

    private static Mesh RandomRock()
    {
        return MeshFactory.GetRock(Random.Range(0, MeshFactory.RockVariants));
    }

    private static Quaternion Yaw(float range)
    {
        return Quaternion.Euler(0f, Random.Range(-range, range), 0f);
    }

    // A box whose texture keeps the same size however big the box is
    private GameObject Box(Transform parent, Vector3 size, Vector3 localPosition, Quaternion rotation,
        Material material, float uvUnit, bool detailed)
    {
        return PlaceMesh(parent, MeshFactory.GetBox(size, uvUnit), localPosition, Vector3.one, rotation, material, detailed);
    }

    // A round column that stands on the given base position
    private GameObject Column(Transform parent, float radius, float height, Vector3 basePosition,
        Material material, float uvUnit, bool detailed)
    {
        return PlaceMesh(parent, MeshFactory.GetColumn(radius, height, uvUnit), basePosition, Vector3.one,
            Quaternion.identity, material, detailed);
    }

    // "detailed" pieces cast and receive shadows. Scenery skips shadows to keep phones
    // running smoothly. Pieces never have colliders.
    private GameObject PlaceMesh(Transform parent, Mesh mesh, Vector3 localPosition, Vector3 scale,
        Quaternion rotation, Material material, bool detailed)
    {
        GameObject piece = new GameObject("Piece");
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = scale;

        piece.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer pieceRenderer = piece.AddComponent<MeshRenderer>();
        pieceRenderer.sharedMaterial = material;

        if (!detailed)
        {
            pieceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pieceRenderer.receiveShadows = false;
        }

        return piece;
    }

    // A built-in shape (sphere, capsule, cylinder) without its collider
    private GameObject Primitive(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale,
        Quaternion rotation, Material material, bool detailed)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = scale;

        Renderer pieceRenderer = piece.GetComponent<Renderer>();
        pieceRenderer.sharedMaterial = material;

        if (!detailed)
        {
            pieceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pieceRenderer.receiveShadows = false;
        }

        Destroy(piece.GetComponent<Collider>());
        return piece;
    }

    private Material Flat(Color color, float smoothness)
    {
        string key = string.Format("flat|{0:F2}|{1:F2}|{2:F2}|{3:F2}", color.r, color.g, color.b, smoothness);

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

    private Material Textured(TextureFactory.Kind kind, Color tint, float smoothness, float tiling)
    {
        string key = string.Format("tex|{0}|{1:F2}|{2:F2}|{3:F2}|{4:F2}|{5:F2}",
            kind, tint.r, tint.g, tint.b, smoothness, tiling);

        Material material;
        if (materialCache.TryGetValue(key, out material)) return material;

        if (templateMaterial == null) return null;

        material = new Material(templateMaterial);
        material.color = tint;
        material.mainTexture = TextureFactory.Get(kind);
        material.mainTextureScale = new Vector2(tiling, tiling);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_Metallic", 0f);

        materialCache[key] = material;
        return material;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }
}