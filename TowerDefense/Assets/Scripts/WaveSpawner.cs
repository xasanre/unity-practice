using System.Collections;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    public Enemy enemyPrefab;
    public Transform[] waypoints;
    public int[] enemiesPerWave = {5, 8, 11};
    public float spawnInterval = 0.8f;
    public float waveInterval = 3f;
    public int wave;
    public int totalSpawned;
    public float countdown;
    public bool allSpawned;
    public bool started;
    IEnumerator Start()
    {
        if (enemyPrefab == null || waypoints == null || waypoints.Length < 2)
        { Debug.LogError("WaveSpawner requires an enemy prefab and at least two waypoints."); enabled = false; yield break; }
        while (!started) { if (GameManager.Instance.Finished) yield break; yield return null; }
        for (int w = 0; w < enemiesPerWave.Length; w++)
        {
            countdown = waveInterval;
            while (countdown > 0)
            {
                if (GameManager.Instance.Finished) yield break;
                countdown -= Time.deltaTime; yield return null;
            }
            wave = w + 1;
            for (int i = 0; i < enemiesPerWave[w]; i++)
            {
                if (GameManager.Instance.Finished) yield break;
                var enemy = Instantiate(enemyPrefab);
                enemy.Initialize(waypoints, GameManager.Instance, 1.8f + w * 0.25f, 2 + w);
                totalSpawned++;
                yield return new WaitForSeconds(spawnInterval);
            }
            while (Enemy.Active.Count > 0)
            { if (GameManager.Instance.Finished) yield break; yield return null; }
        }
        allSpawned = true;
        GameManager.Instance.Win();
    }
    void OnDrawGizmos()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.2f);
            if (i > 0 && waypoints[i - 1] != null) Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
        }
    }
}

