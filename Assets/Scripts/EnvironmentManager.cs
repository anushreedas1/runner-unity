using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Sets up the night sky, mist and moonlight. It creates itself when the game starts
// and re-applies the look every time the scene reloads (for example on Restart).
public class EnvironmentManager : MonoBehaviour
{
    private static EnvironmentManager instance;

    // Sky and fog share one colour, so the far end of the path fades into the dark
    private static readonly Color skyColor = new Color(0.07f, 0.09f, 0.12f);
    private static readonly Color ambientColor = new Color(0.30f, 0.34f, 0.42f);
    private static readonly Color moonColor = new Color(0.62f, 0.72f, 1.00f);

    private const float FogStart = 25f;
    private const float FogEnd = 100f;
    private const float MoonIntensity = 1.1f;

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
        // Mist hides the far end of the path, where new sections appear
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = skyColor;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;

        // Cool, dim light in the shadows
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;

        // Dark sky behind everything
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyColor;
        }

        // Moonlight from the left, which gives the stones some shape
        Light moon = FindSun();
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(38f, 60f, 0f);
            moon.color = moonColor;
            moon.intensity = MoonIntensity;
        }
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