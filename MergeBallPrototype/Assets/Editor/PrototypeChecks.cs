using System;
using UnityEngine;

namespace MergePrototype
{
    public static class PrototypeChecks
    {
        public static string RunRules()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Run PrototypeChecks in Play mode.");

            var randomState = UnityEngine.Random.state;
            var root = new GameObject("__PrototypeRuleChecks");
            root.SetActive(false);
            int assertions = 0;
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
                assertions++;
            }

            try
            {
                var template = new GameObject("TestBall", typeof(SpriteRenderer),
                    typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(MergeBall));
                template.transform.SetParent(root.transform);
                template.SetActive(false);
                var game = root.AddComponent<MergeGame>();
                game.enabled = false;
                game.ballPrefab = template.GetComponent<MergeBall>();
                game.ballRoot = new GameObject("TestBalls").transform;
                game.ballRoot.SetParent(root.transform);
                root.SetActive(true);
                game.RestartGame();

                Check(MergeGame.Radii.Length == 10 && MergeGame.Colors.Length == 10,
                    "Exactly ten radii and colors are required.");
                for (int i = 0; i < 10; i++)
                {
                    Check(MergeGame.Radii[i] > 0, "Radii must be positive.");
                    if (i > 0) Check(MergeGame.Radii[i] > MergeGame.Radii[i - 1], "Radii must increase.");
                    for (int j = 0; j < i; j++)
                        Check(MergeGame.Colors[i] != MergeGame.Colors[j], "Tier colors must differ.");
                }

                for (int tier = 1; tier < 10; tier++)
                {
                    game.RestartGame();
                    var a = game.SpawnBall(tier, new Vector2(-1, -3), true);
                    var b = game.SpawnBall(tier, new Vector2(1, -3), true);
                    Check(game.TryMerge(a, b), "Equal lower tiers must merge.");
                    Check(game.Balls.Count == 1 && game.Balls[0].Tier == tier + 1, "Wrong merge output.");
                    Check(game.Score == (1 << tier), "Wrong merge score.");
                    Check(a.Consumed && b.Consumed && game.Balls[0].Armed, "Merge consumption or arming lost.");
                    Check(!game.TryMerge(a, b), "A consumed pair must not merge again.");
                }

                game.RestartGame();
                var maximumA = game.SpawnBall(10, new Vector2(-1.7f, -3), true);
                var maximumB = game.SpawnBall(10, new Vector2(1.7f, -3), true);
                Check(!game.TryMerge(maximumA, maximumB) && game.Balls.Count == 2 && game.Score == 0,
                    "Tier ten must remain on the board.");
                Check(!maximumA.Consumed && !maximumB.Consumed, "Maximum balls must not be consumed.");

                game.RestartGame();
                var small = game.SpawnBall(1, new Vector2(-1, -3), true);
                var large = game.SpawnBall(2, new Vector2(1, -3), true);
                Check(!game.TryMerge(small, large) && game.Score == 0, "Different tiers must not merge.");
                Check(!game.TryMerge(small, small), "A ball cannot merge with itself.");

                game.RestartGame();
                var first = game.SpawnBall(1, new Vector2(-1, -3), true);
                var second = game.SpawnBall(1, new Vector2(0, -3), true);
                var third = game.SpawnBall(1, new Vector2(1, -3), true);
                Check(game.TryMerge(first, second), "Initial pair must merge.");
                Check(!game.TryMerge(first, third) && !game.TryMerge(second, third), "Three-ball race reused a ball.");
                Check(game.Balls.Count == 2 && game.Score == 2, "Three-ball race duplicated output or score.");

                game.RestartGame();
                int queuedTier = game.NextTier;
                Check(game.DropAt(-100f), "An empty board must accept a drop.");
                var dropped = game.Balls[0];
                Check(dropped.Body.position.x - dropped.Radius > MergeGame.Left,
                    "Drop position must keep the entire ball inside the left wall.");
                Check(game.CurrentTier == queuedTier, "The next preview must become the current ball.");
                Check(!game.DropAt(100f) && game.Balls.Count == 1, "Rapid clicks must respect the cooldown.");
                game.RestartGame();
                Check(game.DropAt(100f), "Restart must reset the cooldown.");
                dropped = game.Balls[0];
                Check(dropped.Body.position.x + dropped.Radius < MergeGame.Right,
                    "Drop position must keep the entire ball inside the right wall.");
                game.RestartGame();
                game.SpawnBall(1, new Vector2(0, MergeGame.SpawnY), true);
                Check(!game.DropAt(0) && game.Balls.Count == 1, "An occupied spawn must not create overlapping balls.");

                game.RestartGame();
                var newborn = game.SpawnBall(1, new Vector2(0, MergeGame.SpawnY));
                game.TickDanger(2.1f);
                Check(!newborn.Armed && !game.GameOver && game.DangerSeconds == 0,
                    "Unarmed newborn must not immediately lose.");
                newborn.Arm();
                newborn.Body.position = new Vector2(0, MergeGame.DangerY - newborn.Radius);
                game.TickDanger(2.1f);
                Check(!game.GameOver && game.DangerSeconds == 0, "Touching the line is not crossing it.");
                newborn.Body.position += Vector2.up * .01f;
                game.TickDanger(1.99f);
                Check(!game.GameOver && game.DangerSeconds > 1.98f, "Failure fired before two seconds.");
                newborn.Body.position -= Vector2.up * .02f;
                game.TickDanger(.1f);
                Check(!game.GameOver && game.DangerSeconds == 0, "Returning below the line must reset danger.");
                newborn.Body.position += Vector2.up * .02f;
                game.TickDanger(2f);
                Check(game.GameOver && Mathf.Approximately(game.DangerSeconds, 2f), "Two seconds must end the game.");
                Check(!game.DropAt(0), "Game over must block dropping.");

                game.RestartGame();
                var highA = game.SpawnBall(1, new Vector2(-.2f, MergeGame.SpawnY), true);
                var highB = game.SpawnBall(1, new Vector2(.2f, MergeGame.SpawnY));
                game.TickDanger(1f);
                Check(game.TryMerge(highA, highB), "High pair must merge.");
                Check(game.Balls[0].Armed && Mathf.Approximately(game.DangerSeconds, 1f),
                    "Merging must retain arming and the global danger timer.");
                game.TickDanger(1f);
                Check(game.GameOver && game.Score == 2, "A high merge must not postpone failure.");

                game.RestartGame();
                Check(!game.GameOver && game.Score == 0 && game.DangerSeconds == 0 && game.Balls.Count == 0,
                    "Restart must clear the complete round state.");
                for (int i = 0; i < 64; i++)
                {
                    game.RestartGame();
                    Check(game.CurrentTier >= 1 && game.CurrentTier <= 3 && game.NextTier >= 1 && game.NextTier <= 3,
                        "Drop queue must only contain tiers one through three.");
                }
                return $"PASS: {assertions} rule assertions. Live collision/input checks are separate.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Random.state = randomState;
            }
        }
    }
}
