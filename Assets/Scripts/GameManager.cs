using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState
{
    Menu,
    Shop,
    Playing,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Lets "Restart" skip the menu (this survives the scene reload)
    private static bool skipMenuOnLoad;

    private struct SkinInfo
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly int Price;

        public SkinInfo(string name, Color color, int price)
        {
            Name = name;
            Color = color;
            Price = price;
        }
    }

    private static readonly SkinInfo[] skins = new SkinInfo[]
    {
        new SkinInfo("Classic", new Color(0.90f, 0.90f, 0.90f), 0),
        new SkinInfo("Ocean",   new Color(0.20f, 0.50f, 1.00f), 100),
        new SkinInfo("Forest",  new Color(0.20f, 0.75f, 0.30f), 150),
        new SkinInfo("Sunset",  new Color(1.00f, 0.45f, 0.20f), 250),
        new SkinInfo("Royal",   new Color(0.55f, 0.25f, 0.90f), 400),
        new SkinInfo("Gold",    new Color(1.00f, 0.82f, 0.10f), 600),
    };

    [SerializeField] private Transform player;
    [SerializeField] private string gameTitle = "ENDLESS RUNNER";

    [Header("Speed")]
    [SerializeField] private float startSpeed = 8f;
    [SerializeField] private float maxSpeed = 18f;
    [SerializeField] private float speedIncreasePerSecond = 0.08f;
    [SerializeField] private float stumbledSpeedFactor = 0.85f;

    [Header("Monster")]
    [SerializeField] private float invulnerableTime = 1.5f;
    [SerializeField] private float recoveryTime = 8f;

    [Header("Power-ups")]
    [SerializeField] private float magnetDuration = 10f;
    [SerializeField] private float shieldDuration = 15f;
    [SerializeField] private float doubleScoreDuration = 10f;
    [SerializeField] private float shieldBreakGraceTime = 1f;

    [Header("Scoring")]
    [SerializeField] private int pointsPerCoin = 10;

    public GameState State { get; private set; }
    public bool IsPlaying => State == GameState.Playing;
    public bool IsGameOver => State == GameState.GameOver;

    public bool IsStumbled { get; private set; }
    public bool IsInvulnerable => invulnerableTimer > 0f;
    public float CurrentSpeed { get; private set; }

    public Transform PlayerTransform => player;
    public bool MagnetActive => magnetTimer > 0f;
    public bool ShieldActive => shieldTimer > 0f;
    public bool DoubleScoreActive => doubleScoreTimer > 0f;
    public float ScoreMultiplier => DoubleScoreActive ? 2f : 1f;

    public int Distance { get; private set; }
    public int Coins { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int TotalCoins { get; private set; }

    public int SelectedSkin { get; private set; }
    public Color SkinColor => skins[SelectedSkin].Color;

    private float startZ;
    private float lastZ;
    private float scoreAccumulator;
    private float runTime;
    private float invulnerableTimer;
    private float stumbleTimer;

    private float magnetTimer;
    private float shieldTimer;
    private float doubleScoreTimer;

    void Awake()
    {
        Instance = this;

        BestScore = PlayerPrefs.GetInt("BestScore", 0);
        TotalCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        CurrentSpeed = startSpeed;

        SelectedSkin = Mathf.Clamp(PlayerPrefs.GetInt("SelectedSkin", 0), 0, skins.Length - 1);
        if (!IsOwned(SelectedSkin)) SelectedSkin = 0;

        bool startPlaying = skipMenuOnLoad;
        skipMenuOnLoad = false;
        State = startPlaying ? GameState.Playing : GameState.Menu;
    }

    void Start()
    {
        if (player != null)
        {
            startZ = player.position.z;
            lastZ = startZ;
        }
    }

    void Update()
    {
        if (player == null) return;

#if UNITY_EDITOR
        HandleDebugKeys();
#endif

        if (State == GameState.Playing)
        {
            UpdateRun();
        }
        else if (State == GameState.GameOver)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
        }
    }

    private void UpdateRun()
    {
        float dt = Time.deltaTime;
        runTime += dt;

        if (invulnerableTimer > 0f) invulnerableTimer -= dt;
        if (magnetTimer > 0f) magnetTimer -= dt;
        if (shieldTimer > 0f) shieldTimer -= dt;
        if (doubleScoreTimer > 0f) doubleScoreTimer -= dt;

        if (IsStumbled)
        {
            stumbleTimer -= dt;
            if (stumbleTimer <= 0f) IsStumbled = false;
        }

        float baseSpeed = Mathf.Min(maxSpeed, startSpeed + runTime * speedIncreasePerSecond);
        CurrentSpeed = IsStumbled ? baseSpeed * stumbledSpeedFactor : baseSpeed;

        // Score grows with distance, and the 2x power-up doubles what is earned
        float z = player.position.z;
        float distanceGained = Mathf.Max(0f, z - lastZ);
        lastZ = z;
        scoreAccumulator += distanceGained * ScoreMultiplier;

        Distance = Mathf.Max(0, Mathf.FloorToInt(z - startZ));
        Score = Mathf.FloorToInt(scoreAccumulator);
    }

    private void StartRun()
    {
        if (player != null)
        {
            startZ = player.position.z;
            lastZ = startZ;
        }

        State = GameState.Playing;
    }

    public void AddCoin()
    {
        if (!IsPlaying) return;

        Coins++;
        scoreAccumulator += pointsPerCoin * ScoreMultiplier;
        Score = Mathf.FloorToInt(scoreAccumulator);
    }

    public void ActivatePowerUp(PowerUpType type)
    {
        if (!IsPlaying) return;

        switch (type)
        {
            case PowerUpType.Magnet:
                magnetTimer = magnetDuration;
                break;
            case PowerUpType.Shield:
                shieldTimer = shieldDuration;
                break;
            case PowerUpType.DoubleScore:
                doubleScoreTimer = doubleScoreDuration;
                break;
        }
    }

    public void PlayerHitObstacle()
    {
        if (!IsPlaying || IsInvulnerable) return;

        if (ShieldActive)
        {
            // The shield takes the hit and breaks
            shieldTimer = 0f;
            invulnerableTimer = shieldBreakGraceTime;
            return;
        }

        if (IsStumbled)
        {
            // Second hit while the monster is right behind you
            GameOver();
            return;
        }

        IsStumbled = true;
        stumbleTimer = recoveryTime;
        invulnerableTimer = invulnerableTime;
    }

    public void GameOver()
    {
        if (State == GameState.GameOver) return;

        State = GameState.GameOver;

        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt("BestScore", BestScore);
        }

        TotalCoins += Coins;
        PlayerPrefs.SetInt("TotalCoins", TotalCoins);
        PlayerPrefs.Save();
    }

    public void Restart()
    {
        skipMenuOnLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMenu()
    {
        skipMenuOnLoad = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Skins ----------

    private bool IsOwned(int index)
    {
        return index == 0 || PlayerPrefs.GetInt("SkinOwned_" + index, 0) == 1;
    }

    private void SelectSkin(int index)
    {
        SelectedSkin = index;
        PlayerPrefs.SetInt("SelectedSkin", index);
        PlayerPrefs.Save();
    }

    private void BuyOrEquipSkin(int index)
    {
        if (IsOwned(index))
        {
            SelectSkin(index);
            return;
        }

        int price = skins[index].Price;
        if (TotalCoins < price) return;

        TotalCoins -= price;
        PlayerPrefs.SetInt("TotalCoins", TotalCoins);
        PlayerPrefs.SetInt("SkinOwned_" + index, 1);
        SelectSkin(index);
    }

#if UNITY_EDITOR
    // Testing helpers (only exist inside the Unity editor):
    //   C      = add 500 coins (in menu or shop)
    //   Delete = erase all saved data (in menu or shop)
    private void HandleDebugKeys()
    {
        if (State != GameState.Menu && State != GameState.Shop) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.cKey.wasPressedThisFrame)
        {
            TotalCoins += 500;
            PlayerPrefs.SetInt("TotalCoins", TotalCoins);
            PlayerPrefs.Save();
        }

        if (kb.deleteKey.wasPressedThisFrame)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            GoToMenu();
        }
    }
#endif

    // ---------- UI ----------

    void OnGUI()
    {
        // Sizes scale with the screen so it works on a phone too
        float unit = Mathf.Max(Mathf.Min(Screen.height / 20f, Screen.width / 16f), 12f);

        switch (State)
        {
            case GameState.Menu:
                DrawMenu(unit);
                break;
            case GameState.Shop:
                DrawShop(unit);
                break;
            case GameState.Playing:
                DrawHud(unit);
                break;
            case GameState.GameOver:
                DrawHud(unit);
                DrawGameOver(unit);
                break;
        }
    }

    private GUIStyle MakeLabel(float fontSize, TextAnchor anchor, Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(fontSize);
        style.fontStyle = FontStyle.Bold;
        style.alignment = anchor;
        style.wordWrap = true;
        style.normal.textColor = color;
        return style;
    }

    private GUIStyle MakeButton(float fontSize)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = Mathf.RoundToInt(fontSize);
        return style;
    }

    private void DrawMenu(float unit)
    {
        float w = Screen.width;

        GUI.Label(new Rect(0f, unit * 2.5f, w, unit * 2.5f), gameTitle,
            MakeLabel(unit * 1.6f, TextAnchor.MiddleCenter, Color.white));

        GUIStyle info = MakeLabel(unit, TextAnchor.MiddleCenter, Color.white);
        GUI.Label(new Rect(0f, unit * 5.5f, w, unit * 1.6f), "Coins: " + TotalCoins, info);
        GUI.Label(new Rect(0f, unit * 7.1f, w, unit * 1.6f), "Best: " + BestScore, info);

        float bw = Mathf.Min(unit * 9f, w * 0.8f);
        float bx = (w - bw) / 2f;
        GUIStyle button = MakeButton(unit);

        if (GUI.Button(new Rect(bx, unit * 9.5f, bw, unit * 2.2f), "PLAY", button))
        {
            StartRun();
        }

        if (GUI.Button(new Rect(bx, unit * 12.5f, bw, unit * 2.2f), "SHOP", button))
        {
            State = GameState.Shop;
        }

        GUI.Label(new Rect(w * 0.1f, unit * 16f, w * 0.8f, unit * 2.5f),
            "Swipe or use the arrow keys to move, jump and slide",
            MakeLabel(unit * 0.6f, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.8f)));
    }

    private void DrawShop(float unit)
    {
        float panelW = Mathf.Min(Screen.width * 0.92f, unit * 17f);
        float panelH = unit * 15.5f;

        // In landscape the panel sits on the right so you can still see your runner
        float panelX = Screen.width > Screen.height
            ? Screen.width - panelW - unit
            : (Screen.width - panelW) / 2f;
        float panelY = (Screen.height - panelH) / 2f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none);

        GUI.Label(new Rect(panelX, panelY + unit * 0.4f, panelW, unit * 1.6f), "SHOP",
            MakeLabel(unit, TextAnchor.MiddleCenter, Color.white));
        GUI.Label(new Rect(panelX, panelY + unit * 2.0f, panelW, unit * 1.4f), "Coins: " + TotalCoins,
            MakeLabel(unit * 0.9f, TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0f)));

        float pad = unit * 0.5f;
        float rowH = unit * 1.65f;
        float firstRowY = panelY + unit * 3.6f;

        GUIStyle nameStyle = MakeLabel(unit * 0.8f, TextAnchor.MiddleLeft, Color.white);
        GUIStyle buttonStyle = MakeButton(unit * 0.7f);
        float buttonW = panelW * 0.36f;

        for (int i = 0; i < skins.Length; i++)
        {
            float ry = firstRowY + i * rowH;
            bool owned = IsOwned(i);
            bool selected = (i == SelectedSkin);

            // Color swatch
            Color previous = GUI.color;
            GUI.color = skins[i].Color;
            GUI.DrawTexture(new Rect(panelX + pad, ry + unit * 0.2f, unit * 1.2f, unit * 1.2f), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(new Rect(panelX + pad + unit * 1.6f, ry, panelW * 0.38f, unit * 1.6f), skins[i].Name, nameStyle);

            string label;
            if (owned) label = selected ? "Equipped" : "Equip";
            else label = "Buy " + skins[i].Price;

            GUI.enabled = owned ? !selected : TotalCoins >= skins[i].Price;

            Rect buttonRect = new Rect(panelX + panelW - pad - buttonW, ry + unit * 0.1f, buttonW, unit * 1.4f);
            if (GUI.Button(buttonRect, label, buttonStyle))
            {
                BuyOrEquipSkin(i);
            }

            GUI.enabled = true;
        }

        float backY = firstRowY + skins.Length * rowH + unit * 0.2f;
        if (GUI.Button(new Rect(panelX + panelW * 0.25f, backY, panelW * 0.5f, unit * 1.6f), "Back", MakeButton(unit * 0.8f)))
        {
            State = GameState.Menu;
        }
    }

    private void DrawHud(float unit)
    {
        GUIStyle hud = MakeLabel(unit, TextAnchor.UpperLeft, Color.white);

        GUI.Label(new Rect(unit, unit * 0.5f, Screen.width, unit * 1.6f), "Score: " + Score, hud);
        GUI.Label(new Rect(unit, unit * 1.9f, Screen.width, unit * 1.6f), "Coins: " + Coins, hud);
        GUI.Label(new Rect(unit, unit * 3.3f, Screen.width, unit * 1.6f), "Best: " + BestScore, hud);

        if (State != GameState.Playing) return;

        if (IsStumbled)
        {
            GUI.Label(new Rect(0f, unit * 0.5f, Screen.width, unit * 1.6f), "MONSTER IS CLOSE!",
                MakeLabel(unit, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.3f)));
        }

        // Active power-ups, top right
        float y = unit * 0.5f;
        if (MagnetActive) DrawPowerUpLabel(ref y, unit, "MAGNET", magnetTimer, new Color(1f, 0.35f, 0.35f));
        if (ShieldActive) DrawPowerUpLabel(ref y, unit, "SHIELD", shieldTimer, new Color(0.3f, 0.9f, 1f));
        if (DoubleScoreActive) DrawPowerUpLabel(ref y, unit, "2X POINTS", doubleScoreTimer, new Color(0.4f, 1f, 0.4f));
    }

    private void DrawPowerUpLabel(ref float y, float unit, string label, float timeLeft, Color color)
    {
        GUIStyle style = MakeLabel(unit, TextAnchor.UpperRight, color);
        GUI.Label(new Rect(0f, y, Screen.width - unit, unit * 1.6f), label + " " + Mathf.CeilToInt(timeLeft) + "s", style);
        y += unit * 1.4f;
    }

    private void DrawGameOver(float unit)
    {
        float panelW = Mathf.Min(Screen.width * 0.8f, unit * 16f);
        float panelH = unit * 13f;
        float panelX = (Screen.width - panelW) / 2f;
        float panelY = (Screen.height - panelH) / 2f;

        GUI.Box(new Rect(panelX, panelY, panelW, panelH), GUIContent.none);

        GUIStyle centered = MakeLabel(unit, TextAnchor.MiddleCenter, Color.white);

        GUI.Label(new Rect(panelX, panelY + unit * 0.5f, panelW, unit * 2f), "CAUGHT!", centered);
        GUI.Label(new Rect(panelX, panelY + unit * 2.5f, panelW, unit * 1.6f), "Score: " + Score, centered);
        GUI.Label(new Rect(panelX, panelY + unit * 4.0f, panelW, unit * 1.6f), "Coins: " + Coins, centered);
        GUI.Label(new Rect(panelX, panelY + unit * 5.5f, panelW, unit * 1.6f), "Best: " + BestScore, centered);

        GUIStyle button = MakeButton(unit);

        if (GUI.Button(new Rect(panelX + panelW * 0.2f, panelY + unit * 7.6f, panelW * 0.6f, unit * 2f), "Restart", button))
        {
            Restart();
        }

        if (GUI.Button(new Rect(panelX + panelW * 0.2f, panelY + unit * 10.4f, panelW * 0.6f, unit * 2f), "Menu", button))
        {
            GoToMenu();
        }
    }
}