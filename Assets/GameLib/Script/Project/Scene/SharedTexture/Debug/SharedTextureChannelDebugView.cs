#nullable enable
using System.Reflection;
using UnityEngine;

namespace Game.SharedTexture
{
    /// <summary>
    /// SharedTextureChannelHub の内容を画面上に表示する debug 用コンポーネント。
    /// ビルドでも動作する。不要時は GameObject を無効にする。
    /// </summary>
    public sealed class SharedTextureChannelDebugView : MonoBehaviour
    {
        [SerializeField] bool showTextures = true;
        [SerializeField] int previewSize = 128;
        [SerializeField] KeyCode toggleKey = KeyCode.F9;

        ISharedTextureChannelHub? _hub;
        bool _visible = true;

        void Start()
        {
            // VContainer からの注入がない場合のフォールバック:
            // MonoBehaviour として配置された場合、FindAnyObjectByType で探す
        }

        /// <summary>外部から Hub を注入する。</summary>
        public void SetHub(ISharedTextureChannelHub hub) => _hub = hub;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible || _hub == null)
                return;

            var service = _hub as SharedTextureChannelHubService;
            if (service == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>SharedTexture Hub</b> ({service.ChannelCount} channels)", CreateRichStyle());

            // リフレクションで内部辞書にアクセス（Debug 専用）
            var channelsField = typeof(SharedTextureChannelHubService)
                .GetField("_channels", BindingFlags.NonPublic | BindingFlags.Instance);

            if (channelsField?.GetValue(service) is System.Collections.IDictionary dict)
            {
                foreach (System.Collections.DictionaryEntry kvp in dict)
                {
                    var tag = kvp.Key as string ?? "?";
                    var entryObj = kvp.Value;
                    if (entryObj == null) continue;

                    var frameField = entryObj.GetType().GetField("Frame");
                    if (frameField == null) continue;
                    var frame = (SharedTextureFrame)frameField.GetValue(entryObj)!;

                    GUILayout.Space(4);
                    GUILayout.Label($"<color=cyan>{tag}</color>", CreateRichStyle());
                    GUILayout.Label($"  Kind: {frame.SourceKind} | Producer: {frame.ProducerTag}");
                    GUILayout.Label($"  Size: {frame.Width}x{frame.Height} | Frame: {frame.FrameId}");

                    if (frame.CameraCapture.HasValue)
                    {
                        var cc = frame.CameraCapture.Value;
                        GUILayout.Label($"  Camera: {(cc.CaptureCamera != null ? cc.CaptureCamera.name : "null")} " +
                                        $"| Ortho: {cc.IsOrthographic} ({cc.OrthographicSize:F1})");
                    }

                    if (showTextures && frame.Texture != null)
                    {
                        GUILayout.Box(frame.Texture, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                    }
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        static GUIStyle? s_RichStyle;
        static GUIStyle CreateRichStyle()
        {
            if (s_RichStyle == null)
            {
                s_RichStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }
            return s_RichStyle;
        }
#endif
    }
}
