using System.Collections;
using UnityEngine;

public class BirdSpawner : MonoBehaviour
{
    public Bird birdPrefab;
    public Rigidbody2D hook;

    [Min(1)]
    public int totalBirds = 5;

    [Min(0f)]
    public float spawnDelay = 2f;

    private int birdsRemaining;
    private bool isWaiting;
    private bool gameEnded;
    private Bird currentBird;

    public int UnlaunchedBirds =>
        birdsRemaining +
        (currentBird != null && !currentBird.HasReleased ? 1 : 0);

    private void Start()
    {
        if (birdPrefab == null || hook == null)
        {
            Debug.LogError("BirdSpawner 没有指定小鸟 Prefab 或 Hook。", this);
            enabled = false;
            return;
        }

        birdsRemaining = totalBirds;
        SpawnBird();
    }

    private void SpawnBird()
    {
        if (birdsRemaining <= 0)
            return;

        Bird bird = Instantiate(
            birdPrefab,
            hook.transform.position,
            Quaternion.identity);

        SpringJoint2D spring = bird.GetComponent<SpringJoint2D>();

        //生成后重新连接
        spring.autoConfigureConnectedAnchor = false;
        spring.connectedBody = hook;
        spring.anchor = Vector2.zero;
        spring.connectedAnchor = Vector2.zero;
        spring.enabled = true;

        bird.spawner = this;
        currentBird = bird;
        birdsRemaining--;

        Debug.Log($"新小鸟已准备好，尚未生成的小鸟：{birdsRemaining}");
    }

    public void QueueNextBird()
    {
        if (gameEnded || isWaiting || birdsRemaining <= 0)
            return;

        StartCoroutine(SpawnAfterDelay());
    }

    private IEnumerator SpawnAfterDelay()
    {
        isWaiting = true;
        yield return new WaitForSeconds(spawnDelay);

        SpawnBird();
        isWaiting = false;
    }

    public void EndLevel()
    {
        gameEnded = true;

        // 取消正在等待的补鸟任务。
        StopAllCoroutines();
        isWaiting = false;

        // 清除弹弓上尚未发射的小鸟，避免结算后还能拖动。
        if (currentBird != null && !currentBird.HasReleased)
            Destroy(currentBird.gameObject);
    }
}