using UnityEngine;

// Shows the frame rate in the bottom left corner.
// Set Visible to false before you release the game.
public class FpsCounter : MonoBehaviour
{
    private static readonly bool Visible = false;

    private float smoothedFps = 60f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!Visible) return;

        GameObject host = new GameObject("FpsCounter");
        host.AddComponent<FpsCounter>();
        DontDestroyOnLoad(host);
    }

    void Update()
    {
        float delta = Time.unscaledDeltaTime;
        if (delta > 0f) smoothedFps = Mathf.Lerp(smoothedFps, 1f / delta, 0.05f);
    }

    void OnGUI()
    {
        float unit = Mathf.Max(Mathf.Min(Screen.height / 20f, Screen.width / 16f), 12f);

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(unit * 0.6f);
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = new Color(0.6f, 1f, 0.6f);

        GUI.Label(new Rect(unit * 0.5f, Screen.height - unit * 1.8f, unit * 8f, unit * 1.4f),
            "FPS: " + Mathf.RoundToInt(smoothedFps), style);
    }
}