using UnityEngine;
using UnityEngine.Rendering;

// Builds real fire out of particles. It needs a particle material (see the setup steps).
public static class FireEffect
{
    private static Texture2D softTexture;

    // A soft round glow, used for every flame particle
    public static Texture2D GetTexture()
    {
        if (softTexture != null) return softTexture;

        const int size = 64;
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float glow = Mathf.Clamp01(1f - distance);
                glow = glow * glow * (3f - 2f * glow);

                byte value = (byte)(glow * 255f);
                pixels[y * size + x] = new Color32(value, value, value, value);
            }
        }

        softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        softTexture.name = "Generated Flame Glow";
        softTexture.wrapMode = TextureWrapMode.Clamp;
        softTexture.filterMode = FilterMode.Bilinear;
        softTexture.SetPixels32(pixels);
        softTexture.Apply(false);
        return softTexture;
    }

    // The flame shoots out along the object's forward direction (blue arrow)
    public static ParticleSystem Create(Transform parent, Vector3 localPosition, Quaternion localRotation,
        Material material, float startSpeed, float lifetime, float startSize, float rate,
        float coneAngle, float coneRadius)
    {
        GameObject fire = new GameObject("Fire");
        fire.transform.SetParent(parent, false);
        fire.transform.localPosition = localPosition;
        fire.transform.localRotation = localRotation;

        ParticleSystem system = fire.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed * 0.8f, startSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.7f, startSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.CeilToInt(rate * lifetime * 1.5f) + 4;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = coneRadius;

        // Colour: white-yellow, then orange, then dark red as the flame fades out
        ParticleSystem.ColorOverLifetimeModule colorModule = system.colorOverLifetime;
        colorModule.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.35f),
                new GradientColorKey(new Color(0.7f, 0.12f, 0.02f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

        // Size: swell quickly, then shrink away
        ParticleSystem.SizeOverLifetimeModule sizeModule = system.sizeOverLifetime;
        sizeModule.enabled = true;

        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.15f));
        sizeModule.size = new ParticleSystem.MinMaxCurve(1f, curve);

        ParticleSystemRenderer particleRenderer = fire.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        system.Play();
        return system;
    }
}