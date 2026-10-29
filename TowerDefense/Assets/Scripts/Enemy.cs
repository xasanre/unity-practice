using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f;
    public int health = 2;
    public static readonly System.Collections.Generic.List<Enemy> Active = new();
    Transform[] points;
    int next;
    bool resolved;
    GameManager game;
    public void Initialize(Transform[] route, GameManager manager, float moveSpeed, int hp)
    {
        points = route; game = manager; speed = moveSpeed; health = hp;
        transform.position = points[0].position; next = 1;
    }
    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }
    void Update()
    {
        if (resolved || points == null || game.Finished) return;
        float step = speed * Time.deltaTime;
        while (next < points.Length)
        {
            Vector3 target = points[next].position;
            float distance = Vector3.Distance(transform.position, target);
            if (distance > step) { transform.position = Vector3.MoveTowards(transform.position, target, step); break; }
            transform.position = target; step -= distance; next++;
        }
        if (next == points.Length) Resolve(false);
    }
    public void TakeDamage(int damage)
    {
        if (resolved) return;
        health -= damage;
        if (health <= 0) Resolve(true);
    }
    void Resolve(bool killed)
    {
        resolved = true;
        game.EnemyResolved(killed);
        Active.Remove(this);
        Destroy(gameObject);
    }
}
