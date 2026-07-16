using UnityEngine;

namespace AerialFlickGame.Core
{
    /// <summary>
    /// 過去 N フレームの (時刻, 位置) 履歴から XY 平面の速度ベクトルを推定する汎用クラス。
    /// 推定は X, Y それぞれ独立の最小二乗線形フィット p(t) = a + b*t の傾き b を速度とする。
    /// MonoBehaviour ではないプレーンな C# クラス。追跡物体からのみ利用する。
    /// </summary>
    public class VelocityEstimator
    {
        private readonly int _maxFrames;
        private readonly float[] _times;
        private readonly Vector2[] _positions;

        private int _count;   // 現在保持しているサンプル数（<= _maxFrames）
        private int _head;    // 次に書き込むリングバッファ位置

        public VelocityEstimator(int maxFrames)
        {
            // 最小二乗フィットには 2 サンプル以上必要なので下限 2 を保証
            _maxFrames = Mathf.Max(2, maxFrames);
            _times = new float[_maxFrames];
            _positions = new Vector2[_maxFrames];
            _count = 0;
            _head = 0;
        }

        /// <summary>Update() 内で毎フレーム呼ぶ。</summary>
        public void AddSample(Vector3 position, float time)
        {
            _times[_head] = time;
            _positions[_head] = new Vector2(position.x, position.y);
            _head = (_head + 1) % _maxFrames;
            if (_count < _maxFrames) _count++;
        }

        /// <summary>XY 平面の速度 [m/s] を返す。サンプル不足時は Vector2.zero。</summary>
        public Vector2 EstimateVelocity()
        {
            // 1 サンプル以下では速度 0
            if (_count < 2) return Vector2.zero;

            // リングバッファを時系列順に走査するための開始インデックス
            int start = (_head - _count + _maxFrames) % _maxFrames;

            // 最小二乗のために平均を取る
            float meanT = 0f;
            Vector2 meanP = Vector2.zero;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _maxFrames;
                meanT += _times[idx];
                meanP += _positions[idx];
            }
            meanT /= _count;
            meanP /= _count;

            // 傾き b = Σ((t-tbar)(p-pbar)) / Σ((t-tbar)^2)
            float sxx = 0f;
            Vector2 sxy = Vector2.zero;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _maxFrames;
                float dt = _times[idx] - meanT;
                Vector2 dp = _positions[idx] - meanP;
                sxx += dt * dt;
                sxy += dt * dp;
            }

            // 時刻がほぼ同一（分母 0）なら速度は求まらない
            if (sxx < 1e-9f) return Vector2.zero;

            return sxy / sxx;
        }

        public void Reset()
        {
            _count = 0;
            _head = 0;
        }
    }
}
