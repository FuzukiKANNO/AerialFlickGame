using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ctrl+Shift+Alt+2 (%#&2) で Game ビューを指定モニタに全画面表示/解除するトグル。
/// finger_from_side の同名スクリプトを移植。
/// </summary>
public static class FullscreenGameView
{
    // ===== 環境に合わせて設定（表示スケールは両モニタ100%前提）=====
    // 出したいモニタの左上座標（ピクセル）。
    // メインが幅1920で2台目が右隣 → (1920, 0)
    // 2台目が左隣なら x はマイナス（例 -1920, 0）
    static readonly Vector2 targetOrigin = new Vector2(1920, 0);
    // 出したいモニタのネイティブ解像度（ピクセル）
    static readonly Vector2 targetResolution = new Vector2(1920, 1080);
    // ============================================================

    static readonly System.Type GameViewType =
        typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    static readonly PropertyInfo ShowToolbarProperty =
        GameViewType.GetProperty("showToolbar", BindingFlags.Instance | BindingFlags.NonPublic);

    const string IdKey = "FullscreenGameView.InstanceID";

    static EditorWindow Recover()
    {
        int id = SessionState.GetInt(IdKey, 0);
        return id != 0 ? EditorUtility.InstanceIDToObject(id) as EditorWindow : null;
    }

    [MenuItem("Window/General/Game (Fullscreen) %#&2", priority = 2)]
    public static void Toggle()
    {
        var instance = Recover();
        if (instance != null)
        {
            instance.Close();
            SessionState.EraseInt(IdKey);
            return;
        }

        instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
        ShowToolbarProperty?.SetValue(instance, false);
        instance.ShowPopup();
        instance.position = new Rect(targetOrigin, targetResolution);
        instance.Focus();

        SessionState.SetInt(IdKey, instance.GetInstanceID());
    }
}
