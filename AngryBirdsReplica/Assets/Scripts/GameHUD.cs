using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameHUD : MonoBehaviour
{
    [Header("数量显示")]
    public BirdSpawner birdSpawner;
    public TMP_Text birdsText;
    public TMP_Text enemiesText;

    [Header("结算界面")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public GameObject nextLevelButton;

    [Header("失败判定")]
    [Min(0f)]
    public float minimumWait = 3f;

    [Min(0.5f)]
    public float settleDuration = 2f;

    private Enemy[] enemies;
    private bool gameEnded;
    private bool checkingFailure;

    private void Start()
    {
        if (birdSpawner == null ||
            birdsText == null ||
            enemiesText == null ||
            resultPanel == null ||
            resultText == null)
        {
            Debug.LogError("GameHUD 的引用没有全部指定。", this);
            enabled = false;
            return;
        }

        resultPanel.SetActive(false);

        enemies = FindObjectsByType<Enemy>(
            FindObjectsSortMode.None);

        if (enemies.Length == 0)
        {
            Debug.LogError(
                "没有找到怪物，请检查怪物是否挂有 Enemy 脚本。",
                this);

            enabled = false;
        }
    }

    private int CountEnemies()
    {
        int count = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null)
                count++;
        }

        return count;
    }

    private void LateUpdate()
    {
        if (gameEnded)
            return;

        int aliveCount = CountEnemies();

        birdsText.SetText(
            "Birds: {0}", birdSpawner.UnlaunchedBirds);

        enemiesText.SetText(
            "Enemies: {0}", aliveCount);

        // 优先判断胜利。
        if (aliveCount == 0)
        {
            ShowResult(true);
            return;
        }

        if (birdSpawner.UnlaunchedBirds == 0 && !checkingFailure)
        {
            checkingFailure = true;
            StartCoroutine(CheckFailure());
        }
    }

    private IEnumerator CheckFailure()
    {
        yield return new WaitForSeconds(minimumWait);

        const float interval = 0.25f;
        float quietTime = 0f;

        while (!gameEnded)
        {
            yield return new WaitForSeconds(interval);

            if (gameEnded)
                yield break;

            if (WorldIsSettled())
                quietTime += interval;
            else
                quietTime = 0f;

            if (quietTime >= settleDuration)
            {
                // 结算前再检查一次，避免错过最后一次击败。
                ShowResult(CountEnemies() == 0);
                yield break;
            }
        }
    }

    private bool WorldIsSettled()
    {
        Rigidbody2D[] bodies = FindObjectsByType<Rigidbody2D>(
            FindObjectsSortMode.None);

        foreach (Rigidbody2D body in bodies)
        {
            if (!body.simulated ||
                body.bodyType != RigidbodyType2D.Dynamic)
            {
                continue;
            }

            // 当前关卡约定的出界范围。
            // 离开这个范围的物体不再影响失败等待。
            Vector2 position = body.position;

            if (position.y < -10f || Mathf.Abs(position.x) > 30f)
                continue;

            if (body.velocity.sqrMagnitude > 0.01f ||
                Mathf.Abs(body.angularVelocity) > 5f)
            {
                return false;
            }
        }

        return true;
    }

    private void ShowResult(bool victory)
    {
        if (gameEnded)
            return;

        gameEnded = true;
        birdSpawner.EndLevel();

        // 结算后停止物理运动，避免失败后又继续撞死怪物。
        Rigidbody2D[] bodies = FindObjectsByType<Rigidbody2D>(
            FindObjectsSortMode.None);

        foreach (Rigidbody2D body in bodies)
            body.simulated = false;

        enemiesText.SetText("Enemies: {0}", CountEnemies());
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        bool hasNextLevel = currentIndex >= 0 &&
            currentIndex + 1 < SceneManager.sceneCountInBuildSettings;

        if (nextLevelButton != null)
            nextLevelButton.SetActive(victory && hasNextLevel);

        resultText.text = victory
            ? (hasNextLevel ? "Victory!" : "Congratulations!")
            : "Defeat!";
        resultPanel.SetActive(true);
    }

    public void NextLevel()
    {
        if (!gameEnded || CountEnemies() != 0)
            return;

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;
        if (currentIndex >= 0 &&
            nextIndex < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(nextIndex);
    }

    public void Retry()
    {
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }
}