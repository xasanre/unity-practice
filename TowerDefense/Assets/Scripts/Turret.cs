using UnityEngine;

public class Turret : MonoBehaviour
{
    public Transform rotatingPart;
    public Transform firePoint;
    public Bullet bulletPrefab;
    public float range = 6f;
    public float fireRate = 1.8f;
    public float turnSpeed = 240f;
    public Enemy target;
    public int shotsFired;
    float cooldown;
    public static Enemy FindNearest(Vector3 position, float radius)
    {
        Enemy nearest = null;
        float best = radius * radius;
        foreach (Enemy enemy in Enemy.Active)
        {
            if (enemy == null || enemy.health <= 0) continue;
            float distance = (enemy.transform.position - position).sqrMagnitude;
            if (distance <= best) { best = distance; nearest = enemy; }
        }
        return nearest;
    }
    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.Finished) return;
        cooldown -= Time.deltaTime;
        target = FindNearest(transform.position, range);
        if (target == null) return;
        Vector3 direction = target.transform.position - rotatingPart.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion aim = Quaternion.LookRotation(direction);
        rotatingPart.rotation = Quaternion.RotateTowards(rotatingPart.rotation, aim, turnSpeed * Time.deltaTime);
        if (cooldown <= 0 && Quaternion.Angle(rotatingPart.rotation, aim) < 8f)
        {
            Instantiate(bulletPrefab, firePoint.position, firePoint.rotation).Seek(target);
            shotsFired++;
            cooldown = 1f / Mathf.Max(0.1f, fireRate);
        }
    }
    void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, range); }
}
