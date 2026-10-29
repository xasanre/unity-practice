using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 14f;
    public int damage = 1;
    public ParticleSystem impactPrefab;
    Enemy target;
    public void Seek(Enemy enemy) { target = enemy; Destroy(gameObject, 5f); }
    void Update()
    {
        if (target == null || GameManager.Instance == null || GameManager.Instance.Finished) { Destroy(gameObject); return; }
        Vector3 destination = target.transform.position;
        float step = speed * Time.deltaTime;
        if (Vector3.Distance(transform.position, destination) <= step + 0.15f)
        {
            if (impactPrefab != null) { var effect = Instantiate(impactPrefab, destination, Quaternion.identity); effect.Play(); Destroy(effect.gameObject, 1.5f); }
            target.TakeDamage(damage);
            GameManager.Instance.hits++;
            Destroy(gameObject);
            return;
        }
        transform.position = Vector3.MoveTowards(transform.position, destination, step);
        transform.LookAt(destination);
    }
}
