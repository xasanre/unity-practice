using UnityEngine;

namespace MergePrototype
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class MergeBall : MonoBehaviour
    {
        public int Tier { get; private set; }
        public float Radius { get; private set; }
        public bool Armed { get; private set; }
        public bool Consumed { get; private set; }
        public Rigidbody2D Body { get; private set; }
        internal MergeGame Owner { get; private set; }
        float age;

        public void Initialize(MergeGame owner, int tier, bool armed)
        {
            Owner = owner;
            Tier = tier;
            Radius = MergeGame.Radii[tier - 1];
            Armed = armed;
            Consumed = false;
            age = 0f;
            transform.localScale = Vector3.one * (Radius * 2f);
            GetComponent<SpriteRenderer>().color = MergeGame.Colors[tier - 1];
            GetComponent<CircleCollider2D>().radius = .5f;
            Body = GetComponent<Rigidbody2D>();
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.mass = Mathf.Max(.1f, Radius * Radius);
            Body.simulated = true;
        }

        void FixedUpdate()
        {
            if (Consumed || Armed || Owner == null || Owner.GameOver) return;
            age += Time.fixedDeltaTime;
            // A full pile may stop a new ball above the line before it ever enters the bin.
            if (Body.position.y + Radius <= MergeGame.DangerY || age >= .9f) Arm();
        }

        public void Arm()
        {
            if (!Consumed) Armed = true;
        }

        internal void Consume()
        {
            Consumed = true;
            Body.simulated = false;
            gameObject.SetActive(false);
        }

        void OnCollisionEnter2D(Collision2D collision) => HandleCollision(collision);
        void OnCollisionStay2D(Collision2D collision) => HandleCollision(collision);

        void HandleCollision(Collision2D collision)
        {
            if (Consumed || Owner == null || Owner.GameOver) return;
            var other = collision.collider.GetComponent<MergeBall>();
            if (other != null && other.Owner == Owner && !other.Consumed)
            {
                Arm();
                other.Arm();
                Owner.TryMerge(this, other);
                return;
            }
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.y > .5f) Arm();
        }
    }
}
