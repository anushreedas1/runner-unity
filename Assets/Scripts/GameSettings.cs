using UnityEngine;

// Settings that apply as soon as the game starts.
public static class GameSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // Phones default to 30 fps unless the game asks for more
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // Don't let the screen turn off while you're playing
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}