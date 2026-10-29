using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public WaveSpawner spawner;
    public UnityEngine.UI.Text statusText;
    public UnityEngine.UI.Text messageText;
    public UnityEngine.UI.Button startButton;
    public GameObject resultPanel;
    public UnityEngine.UI.Text resultTitle;
    public UnityEngine.UI.Text resultStats;
    public int lives = 5;
    #if UNITY_EDITOR
        [Header("Editor Testing")]
        [Range(1f, 10f)]
    public float testSpeed = 1f;
    #endif
    public bool heavySelected;
    public UnityEngine.UI.Image standardSelector;
    public UnityEngine.UI.Image heavySelector;
    public string SelectedTowerName => heavySelected ? "Heavy" : "Standard";
    public void SelectStandard() { heavySelected = false; Notify("Standard: 100 gold | Damage 1 | Rate 1.8/s | Range 6"); }
    public void SelectHeavy() { heavySelected = true; Notify("Heavy: 150 gold | Damage 4 | Rate 0.6/s | Range 8"); }
    public int gold = 200;
    public int killReward = 15;
    public int kills;
    public int hits;
    public int escaped;
    public bool Finished { get; private set; }
    public bool Won { get; private set; }
    string notice;
    float noticeUntil;
    void Awake() { Instance = this; Enemy.Active.Clear(); Time.timeScale = 1; if (resultPanel != null) resultPanel.SetActive(false); }
    void Update()
    {
        #if UNITY_EDITOR
            Time.timeScale = Finished ? 1f : testSpeed;
        #endif
        if (statusText != null && spawner != null)
            statusText.text = $"WAVE {spawner.wave}/{spawner.enemiesPerWave.Length}   LIVES {lives}   KILLS {kills}   GOLD {gold}";
        if (!Finished && messageText != null && spawner != null)
            messageText.text = Time.unscaledTime < noticeUntil ? notice :
                !spawner.started ? $"Selected: {SelectedTowerName} ({(heavySelected ? 150 : 100)} gold) - click a pad, then Start Waves" :
                spawner.countdown > 0 ? $"Next wave in {Mathf.CeilToInt(spawner.countdown)}" : "Build on empty pads - each kill earns 15 gold";
        if (standardSelector != null) standardSelector.color = !heavySelected ? new Color(0.18f,0.48f,0.58f) : new Color(0.12f,0.18f,0.22f);
        if (heavySelector != null) heavySelector.color = heavySelected ? new Color(0.65f,0.38f,0.12f) : new Color(0.12f,0.18f,0.22f);
        if (startButton != null) startButton.interactable = !Finished && spawner != null && !spawner.started;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
    }
    public void Notify(string text) { notice = text; noticeUntil = Time.unscaledTime + 2.5f; }
    public bool TrySpend(int amount)
    {
        if (Finished || amount <= 0 || gold < amount) return false;
        gold -= amount; return true;
    }
    public void StartWaves()
    {
        if (Finished || spawner.started) return;
        if (Object.FindObjectsByType<Turret>(FindObjectsSortMode.None).Length == 0)
        { Notify("Build at least one turret before starting"); return; }
        spawner.started = true;
        noticeUntil = 0;
    }
    public void EnemyResolved(bool killed)
    {
        if (Finished) return;
        if (killed) { kills++; gold += killReward; }
        else { escaped++; lives = Mathf.Max(0, lives - 1); if (lives == 0) Finish(false); }
    }
    public void Win() { if (!Finished) Finish(true); }
    void Finish(bool won)
    {
        if (Finished) return;
        Finished = true; Won = won;
        if (resultTitle != null)
        {
            resultTitle.text = won ? "VICTORY!" : "DEFEAT!";
            resultTitle.color = won ? new Color(0.45f, 0.9f, 0.65f) : new Color(1f, 0.4f, 0.35f);
        }
        if (resultStats != null)
            resultStats.text = won ? "All waves cleared. The base is safe." : "The base was overrun. Try a new defense.";
        if (resultPanel != null) { resultPanel.transform.SetAsLastSibling(); resultPanel.SetActive(true); }
        if (messageText != null) messageText.text = won ? "VICTORY - All waves cleared!" : "DEFEAT - The base was overrun";
    }
    public void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    void OnDestroy() { if (Instance == this) Instance = null; }
}



