using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AerialFlickGame.Game
{
    /// <summary>
    /// シングルトン。スコア・残機を管理し、R キーでリスタートする。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("ゲーム設定")]
        [Tooltip("ライフ制を使う。false ならミスしてもゲームオーバーにならず無限にプレイ")]
        public bool UseLives = true;
        public int StartLives = 5;

        [Header("状態")]
        public bool IsPlaying = true;
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Misses { get; private set; }

        [Header("UI（任意）")]
        public Text ScoreText;
        public Text LivesText;
        public Text StatusText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ResetGame();
        }

        private void Update()
        {
            // R キーでリスタート（New Input System）
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetGame();
            }
        }

        public void ResetGame()
        {
            Score = 0;
            Misses = 0;
            Lives = StartLives;
            IsPlaying = true;
            RefreshUI();
        }

        public void RegisterHit()
        {
            if (!IsPlaying) return;
            Score++;
            RefreshUI();
        }

        public void RegisterMiss()
        {
            if (!IsPlaying) return;
            Misses++;

            if (UseLives)
            {
                Lives--;
                if (Lives <= 0)
                {
                    Lives = 0;
                    IsPlaying = false; // ライフ切れでゲームオーバー
                }
            }
            // ライフ制なしのときは Misses を数えるだけで終了しない

            RefreshUI();
        }

        private void RefreshUI()
        {
            if (ScoreText != null) ScoreText.text = $"Score: {Score}";
            if (LivesText != null)
                LivesText.text = UseLives ? $"Lives: {Lives}" : $"Miss: {Misses}";
            if (StatusText != null)
                StatusText.text = IsPlaying ? "PLAYING  (R: Restart)" : "GAME OVER  (R: Restart)";
        }
    }
}
