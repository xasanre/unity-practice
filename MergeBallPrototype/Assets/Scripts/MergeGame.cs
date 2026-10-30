using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MergePrototype
{
    public sealed class MergeGame : MonoBehaviour
    {
        public const float Left = -3.75f, Right = 3.75f, Floor = -5.9f;
        public const float DangerY = 4.25f, SpawnY = 5.25f;
        public static readonly float[] Radii = { .22f, .29f, .38f, .49f, .62f, .78f, .96f, 1.17f, 1.40f, 1.67f };
        public static readonly Color[] Colors =
        {
            new Color32(245, 107, 111, 255), new Color32(255, 164, 77, 255),
            new Color32(245, 209, 75, 255), new Color32(157, 207, 90, 255),
            new Color32(62, 178, 139, 255), new Color32(71, 193, 209, 255),
            new Color32(79, 139, 218, 255), new Color32(124, 101, 201, 255),
            new Color32(199, 105, 193, 255), new Color32(118, 64, 105, 255)
        };

        public MergeBall ballPrefab;
        public Transform ballRoot;
        public SpriteRenderer preview;
        public LineRenderer guide;
        public Camera gameplayCamera;
        public int Score { get; private set; }
        public int NextTier { get; private set; }
        public int CurrentTier { get; private set; }
        public bool GameOver { get; private set; }
        public float DangerSeconds { get; private set; }
        public IReadOnlyList<MergeBall> Balls => balls;

        readonly List<MergeBall> balls = new List<MergeBall>();
        float nextDropTime;
        float aimX;

        void Start() => RestartGame();

        void Update()
        {
            TickDanger(Time.deltaTime);
            if (GameOver || Mouse.current == null || gameplayCamera == null) return;

            Vector2 screen = Mouse.current.position.ReadValue();
            if (!gameplayCamera.pixelRect.Contains(screen)) return;
            Vector3 world = gameplayCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -gameplayCamera.transform.position.z));
            if (world.y < Floor || world.y > 5.9f) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            aimX = Mathf.Clamp(world.x, Left + Radii[CurrentTier - 1] + .02f, Right - Radii[CurrentTier - 1] - .02f);
            UpdatePreview();
            if (Mouse.current.leftButton.wasPressedThisFrame) DropAt(aimX);
        }

        public void RestartGame()
        {
            foreach (var ball in balls)
            {
                if (ball == null) continue;
                ball.Consume();
                Destroy(ball.gameObject);
            }
            balls.Clear();
            Score = 0;
            DangerSeconds = 0f;
            GameOver = false;
            CurrentTier = UnityEngine.Random.Range(1, 4);
            NextTier = UnityEngine.Random.Range(1, 4);
            nextDropTime = Time.time;
            aimX = 0f;
            UpdatePreview();
        }

        public MergeBall SpawnBall(int tier, Vector2 position, bool armed = false)
        {
            if (tier < 1 || tier > Radii.Length) throw new ArgumentOutOfRangeException(nameof(tier));
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y)) throw new ArgumentOutOfRangeException(nameof(position));
            if (ballPrefab == null) throw new InvalidOperationException("Assign the ball prefab before spawning.");
            var ball = Instantiate(ballPrefab, position, Quaternion.identity, ballRoot != null ? ballRoot : transform);
            ball.Initialize(this, tier, armed);
            ball.gameObject.SetActive(true);
            balls.Add(ball);
            return ball;
        }

        public bool DropAt(float worldX)
        {
            if (GameOver || CurrentTier == 0 || !float.IsFinite(worldX) || Time.time < nextDropTime) return false;
            float radius = Radii[CurrentTier - 1];
            var position = new Vector2(Mathf.Clamp(worldX, Left + radius + .02f, Right - radius - .02f), SpawnY);
            foreach (var ball in balls)
            {
                if (ball == null || ball.Consumed) continue;
                float clearance = radius + ball.Radius + .02f;
                if ((ball.Body.position - position).sqrMagnitude < clearance * clearance) return false;
            }
            SpawnBall(CurrentTier, position);
            nextDropTime = Time.time + .35f;
            CurrentTier = NextTier;
            NextTier = UnityEngine.Random.Range(1, 4);
            aimX = position.x;
            UpdatePreview();
            return true;
        }

        public bool TryMerge(MergeBall a, MergeBall b)
        {
            if (GameOver || a == null || b == null || a == b || a.Owner != this || b.Owner != this ||
                a.Consumed || b.Consumed || a.Tier != b.Tier || a.Tier >= Radii.Length) return false;

            int tier = a.Tier + 1;
            float radius = Radii[tier - 1];
            Vector2 position = (a.Body.position + b.Body.position) * .5f;
            Vector2 velocity = (a.Body.linearVelocity + b.Body.linearVelocity) * .5f;
            position.x = Mathf.Clamp(position.x, Left + radius + .02f, Right - radius - .02f);
            position.y = Mathf.Max(position.y, Floor + radius + .02f);
            bool armed = a.Armed || b.Armed;
            // Destroy is deferred; claim both balls now so simultaneous contacts cannot reuse them.
            a.Consume();
            b.Consume();
            balls.Remove(a);
            balls.Remove(b);
            var result = SpawnBall(tier, position, armed);
            result.Body.linearVelocity = velocity;
            Score += 1 << (tier - 1);
            Destroy(a.gameObject);
            Destroy(b.gameObject);
            return true;
        }

        public void TickDanger(float dt)
        {
            if (!float.IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (GameOver) return;
            bool aboveLine = false;
            foreach (var ball in balls)
            {
                // Float rounding at exact contact must not start the overflow timer.
                if (ball != null && !ball.Consumed && ball.Armed && ball.Body.position.y + ball.Radius > DangerY + .0001f)
                {
                    aboveLine = true;
                    break;
                }
            }
            DangerSeconds = aboveLine ? Mathf.Min(2f, DangerSeconds + dt) : 0f;
            if (DangerSeconds < 2f) return;
            GameOver = true;
            foreach (var ball in balls)
                if (ball != null) ball.Body.simulated = false;
            UpdatePreview();
        }

        void UpdatePreview()
        {
            float radius = Radii[CurrentTier - 1];
            aimX = Mathf.Clamp(aimX, Left + radius + .02f, Right - radius - .02f);
            if (preview != null)
            {
                preview.enabled = !GameOver;
                preview.color = Colors[CurrentTier - 1];
                preview.transform.position = new Vector3(aimX, SpawnY, 0f);
                preview.transform.localScale = Vector3.one * (radius * 2f);
            }
            if (guide != null)
            {
                guide.enabled = !GameOver;
                guide.useWorldSpace = true;
                guide.positionCount = 2;
                guide.SetPosition(0, new Vector3(aimX, SpawnY - radius, 0f));
                guide.SetPosition(1, new Vector3(aimX, Floor + .02f, 0f));
            }
        }
    }
}
