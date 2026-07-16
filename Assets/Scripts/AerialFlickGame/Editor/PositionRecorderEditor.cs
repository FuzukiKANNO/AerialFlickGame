using UnityEngine;
using UnityEditor;
using AerialFlickGame.Recording;

namespace AerialFlickGame.EditorTools
{
    /// <summary>PositionRecorder の Inspector に Start/Stop ボタンと状態表示を追加。</summary>
    [CustomEditor(typeof(PositionRecorder))]
    public class PositionRecorderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var recorder = (PositionRecorder)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("記録状態",
                recorder.IsRecording ? "● Recording..." : "停止中");

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Play 中に記録できます。", MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Start Recording"))
                        recorder.StartRecording();
                    if (GUILayout.Button("Stop Recording"))
                        recorder.StopRecording();
                }
            }
        }
    }
}
