using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(SpringJoint2D))]
public class Bird : MonoBehaviour
{
    [Header("拖拽与发射")]
    public float maxDragDistance = 2f;
    public float releaseDelay = 0.12f;
    [HideInInspector]
    public BirdSpawner spawner;

    private Rigidbody2D rb;
    private SpringJoint2D spring;
    private Camera mainCamera;
    private bool isDragging;
    private bool hasReleased;
    public bool HasReleased => hasReleased;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spring = GetComponent<SpringJoint2D>();
        mainCamera = Camera.main;
    }

    private void OnMouseDown()
    {
        // 已发射的小鸟不能再次拖动。
        if (hasReleased)
            return;

        isDragging = true;

        // 拖动时暂时不让重力和弹簧改变小鸟位置。
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Update()
    {
        if (!isDragging)
            return;

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z =
            transform.position.z - mainCamera.transform.position.z;

        Vector2 mouseWorld =
            mainCamera.ScreenToWorldPoint(mousePosition);

        Vector2 anchorPosition =
            spring.connectedBody.transform.TransformPoint(
                spring.connectedAnchor);

        Vector2 offset = mouseWorld - anchorPosition;

        // 限制小鸟与固定点之间的最大距离。
        Vector2 limitedOffset =
            Vector2.ClampMagnitude(offset, maxDragDistance);

        rb.position = anchorPosition + limitedOffset;

        Debug.Assert(
            limitedOffset.magnitude <= maxDragDistance + 0.001f,
            "小鸟超过了最大拖拽距离。");

        if (Input.GetMouseButtonUp(0))
            Release();
    }

    private void Release()
    {
        isDragging = false;

        Vector2 anchorPosition =
            spring.connectedBody.transform.TransformPoint(
                spring.connectedAnchor);

        // 仅点击、没有明显拉开时，不消耗这只鸟。
        if (Vector2.Distance(rb.position, anchorPosition) < 0.15f)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            return;
        }

        hasReleased = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        StartCoroutine(DetachAfterDelay());
    }

    private IEnumerator DetachAfterDelay()
    {
        yield return new WaitForSeconds(releaseDelay);
        spring.enabled = false;

        // 脱离弹弓后，通知生成器准备下一只鸟。
        if (spawner != null)
            spawner.QueueNextBird();
    }
}