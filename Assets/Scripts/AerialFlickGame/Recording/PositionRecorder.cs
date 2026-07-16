using System.IO;
using UnityEngine;
using AerialFlickGame.Core;
using AerialFlickGame.TrackedObjects;

namespace AerialFlickGame.Recording
{
    /// <summary>
    /// 追跡物体の位置・速度・トラッキング状態を CSV に記録する（finger_from_side の RecordPosition 相当）。
    /// 任意で PredictiveHitDetector のヒットイベントも同じ CSV に記録する。
    /// エディタの Inspector に Start/Stop ボタンを表示（PositionRecorderEditor）。
    /// </summary>
    public class PositionRecorder : MonoBehaviour
    {
        [Header("記録対象")]
        [Tooltip("記録する追跡物体")]
        public TrackedObjectBase TrackedObject;

        [Tooltip("基準オブジェクト（任意）。設定すると相対位置を記録する")]
        public Transform ReferenceObject;

        [Tooltip("任意。設定するとヒット発火も CSV に記録する")]
        public PredictiveHitDetector Detector;

        [Header("出力")]
        [Tooltip("Assets 直下の出力フォルダ名")]
        public string OutputDir = "CSVFiles";

        [Tooltip("Play 開始と同時に記録を始める")]
        public bool RecordOnPlay = false;

        [Header("実験条件（ファイル名・記録用）")]
        public int SubjectNo = 1;
        [Tooltip("自由記述ラベル（ファイル名に入る）")]
        public string Condition = "";

        public bool IsRecording { get; private set; }

        private StreamWriter _writer;
        private int _rowCount;
        private string _pendingEvent = "";

        private void Awake()
        {
            if (TrackedObject == null) TrackedObject = FindFirstObjectByType<TrackedObjectBase>();
            if (Detector == null) Detector = FindFirstObjectByType<PredictiveHitDetector>();
        }

        private void Start()
        {
            if (RecordOnPlay) StartRecording();
        }

        private void Update()
        {
            if (!IsRecording || TrackedObject == null) return;
            WriteRow(_pendingEvent);
            _pendingEvent = "";
        }

        public void StartRecording()
        {
            if (IsRecording) return;
            if (TrackedObject == null)
            {
                Debug.LogWarning("[PositionRecorder] TrackedObject 未設定のため記録を開始できません。");
                return;
            }

            string dir = Path.Combine(Application.dataPath, OutputDir);
            Directory.CreateDirectory(dir);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string cond = string.IsNullOrEmpty(Condition) ? "" : "_" + Condition;
            string fileName = $"subj{SubjectNo:00}{cond}_{timestamp}.csv";
            string filePath = Path.Combine(dir, fileName);

            _writer = new StreamWriter(filePath, false);
            _writer.WriteLine("Time,pos.x,pos.y,pos.z,vel.x,vel.y,isTracking,event");

            // ヒットイベントも記録する
            if (Detector != null) Detector.OnHitDetected += OnHitDetected;

            IsRecording = true;
            _rowCount = 0;
            _pendingEvent = "";
            Debug.Log($"[PositionRecorder] Start recording: {fileName}");
        }

        public void StopRecording()
        {
            if (!IsRecording) return;

            if (Detector != null) Detector.OnHitDetected -= OnHitDetected;

            _writer?.Flush();
            _writer?.Close();
            _writer = null;
            IsRecording = false;

            if (_rowCount == 0)
                Debug.LogWarning("[PositionRecorder] データが記録されませんでした。");
            else
                Debug.Log($"[PositionRecorder] Stop recording ({_rowCount} rows).");
        }

        private void WriteRow(string ev)
        {
            Vector3 pos = TrackedObject.Position;
            if (ReferenceObject != null) pos -= ReferenceObject.position;
            Vector2 v = TrackedObject.Velocity;

            _writer.WriteLine(
                $"{Time.time:F4},{pos.x:F5},{pos.y:F5},{pos.z:F5}," +
                $"{v.x:F5},{v.y:F5},{(TrackedObject.IsTracking ? 1 : 0)},{ev}");
            _rowCount++;
        }

        private void OnHitDetected(HitPrediction pred)
        {
            // 次フレームの行に載せるイベント文字列（; はCSV区切りと衝突しないよう使用）
            _pendingEvent =
                $"HIT lead={pred.TimeToCollision * 1000f:F1}ms vt={pred.VTracked:F3} " +
                $"vrel={pred.VRelative:F3} margin={pred.CollisionMarginUsed * 100f:F2}cm";
        }

        private void OnApplicationQuit()
        {
            if (IsRecording) StopRecording();
        }

        private void OnDisable()
        {
            if (IsRecording) StopRecording();
        }
    }
}
