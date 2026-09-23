using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("击败所需的碰撞相对速度")]
    [Min(0.1f)]
    public float defeatSpeed = 2f;

    [Header("死亡粒子预制体")]
    public GameObject deathEffect;

    private bool isDefeated;

    [Header("出界缓冲距离")]
    [Min(0f)]
    public float outOfBoundsMargin = 1f;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (isDefeated || mainCamera == null)
            return;

        // 当前关卡使用固定的正交相机。
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 position = transform.position;

        bool outsideLeft =
            position.x < cameraPosition.x - halfWidth - outOfBoundsMargin;

        bool outsideRight =
            position.x > cameraPosition.x + halfWidth + outOfBoundsMargin;

        bool belowScreen =
            position.y < cameraPosition.y - halfHeight - outOfBoundsMargin;

        if (outsideLeft || outsideRight || belowScreen)
            Defeat();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDefeated)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        Debug.Log(
            $"{name} 碰撞速度：{impactSpeed:F2}",
            gameObject);

        if (impactSpeed >= defeatSpeed)
            Defeat();
    }

    private void Defeat()
    {
        if (isDefeated)
            return;
            
        // 防止同一帧的多次碰撞重复生成特效。
        isDefeated = true;

        if (deathEffect != null)
        {
            Instantiate(
                deathEffect,
                transform.position,
                Quaternion.identity);
        }

        Destroy(gameObject);
    }
}