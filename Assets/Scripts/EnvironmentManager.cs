using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Sets up the sky, mist and sunlight. It creates itself when the game starts and
// re-applies the look every time the scene reloads (for example on Restart).
//
// If the scene's skybox material is named "DuskSky", you get a sunset over a sea of mist.
// Otherwise you get the dark moonlit night.
public class EnvironmentManager : MonoBehaviour
{
    private static EnvironmentManager instance;
    private static Material skyInstance;

    // Dusk (used with the "DuskSky" skybox)
    private static readonly Color duskMistFallback = new Color(0.36f, 0.33f, 0.33f);
    private static readonly Color duskSun = new Color(1.00f, 0.68f, 0.42f);
    private static readonly Color duskAmbientSky = new Color(0.62f, 0.66f, 0.78f);
    private static readonly Color duskAmbientEquator = new Color(0.45f, 0.40f, 0.40f);
    private static readonly Color duskAmbientGround = new Color(0.20f, 0.17f, 0.16f);
    private const float DuskSunIntensity = 1.25f;

    // Night (used when there is no "DuskSky")
    private static readonly Color nightColor = new Color(0.07f, 0.09f, 0.12f);
    private static readonly Color nightAmbient = new Color(0.30f, 0.34f, 0.42f);
    private static readonly Color moonColor = new Color(0.62f, 0.72f, 1.00f);
    private const float MoonIntensity = 1.1f;

    private const float FogStart = 22f;
    private const float FogEnd = 95f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance != null) return;

        GameObject host = new GameObject("EnvironmentManager");
        host.AddComponent<EnvironmentManager>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    void OnDestroy()
    {
        if (instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    private void Apply()
    {
        Material sky = RenderSettings.skybox;
        bool dusk = sky != null && sky.name == "DuskSky";

        // With the Panoramic shader, the sky is painted by code. With the Procedural shader,
        // the skybox is left as it is.
        bool paintedSky = dusk && PrepareSky(sky);

        Color fogColor;
        if (paintedSky) fogColor = SkyFactory.MistColor;
        else if (dusk) fogColor = duskMistFallback;
        else fogColor = nightColor;

        // Mist hides the far end of the path, where new sections appear
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;

        if (dusk)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = duskAmbientSky;
            RenderSettings.ambientEquatorColor = duskAmbientEquator;
            RenderSettings.ambientGroundColor = duskAmbientGround;
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = nightAmbient;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = dusk ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = fogColor;
        }

        Light sun = FindSun();
        if (sun != null)
        {
            if (dusk)
            {
                // Low sun ahead and to the left: warm glow and long shadows
                sun.transform.rotation = Quaternion.Euler(12f, 125f, 0f);
                sun.color = duskSun;
                sun.intensity = DuskSunIntensity;
            }
            else
            {
                sun.transform.rotation = Quaternion.Euler(38f, 60f, 0f);
                sun.color = moonColor;
                sun.intensity = MoonIntensity;
            }
        }
    }

    // Puts the painted sunset on the sky material. Returns false if the material
    // does not use the Panoramic shader.
    private static bool PrepareSky(Material sky)
    {
        if (!sky.HasProperty("_MainTex")) return false;

        if (skyInstance == null)
        {
            skyInstance = new Material(sky);
            skyInstance.name = "DuskSky";
            skyInstance.SetTexture("_MainTex", SkyFactory.Get());
        }

        RenderSettings.skybox = skyInstance;
        return true;
    }

    private static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;

        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light candidate in lights)
        {
            if (candidate.type == LightType.Directional) return candidate;
        }

        return null;
    }
}