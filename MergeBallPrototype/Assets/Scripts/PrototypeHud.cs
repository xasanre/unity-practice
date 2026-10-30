using TMPro;
using UnityEngine;

namespace MergePrototype
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public MergeGame game;
        public TMP_Text score, countdown, finalScore;
        public UnityEngine.UI.Image nextBall;
        public UnityEngine.UI.Button restart, replay;
        public GameObject endPanel;
        public SpriteRenderer[] dangerDashes;
        readonly Color quietLine = new Color(.67f, .55f, .47f);
        readonly Color dangerLine = new Color(.82f, .23f, .19f);

        void Awake()
        {
            restart.onClick.AddListener(game.RestartGame);
            replay.onClick.AddListener(game.RestartGame);
        }

        void Update()
        {
            score.text = game.Score.ToString();
            int tier = Mathf.Clamp(game.NextTier, 1, 3);
            nextBall.color = MergeGame.Colors[tier - 1];
            nextBall.rectTransform.sizeDelta = Vector2.one * MergeGame.Radii[tier - 1] * 100;
            bool danger = game.DangerSeconds > 0 && !game.GameOver;
            countdown.text = danger ? Mathf.Max(0, 2f - game.DangerSeconds).ToString("0.0") : "";
            Color line = danger || game.GameOver ? dangerLine : quietLine;
            foreach (var dash in dangerDashes) dash.color = line;
            if (endPanel.activeSelf != game.GameOver) endPanel.SetActive(game.GameOver);
            if (game.GameOver) finalScore.text = game.Score.ToString();
        }
    }
}
