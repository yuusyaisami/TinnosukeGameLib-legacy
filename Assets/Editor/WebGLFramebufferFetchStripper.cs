using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// WebGL 2.0 は GL_EXT_shader_framebuffer_fetch をサポートしていないため、
/// UNITY_FRAMEBUFFER_FETCH_AVAILABLE を含むシェーダーバリアントを
/// WebGL ビルド時にストリップする。
/// </summary>
public class WebGLFramebufferFetchStripper : IPreprocessShaders
{
    const string WebGLUnsafeShaderName = "Game/Base/Surface2D_Lit_Fx";
    const string WebGLAllowedPassName = "Universal2D_WebGL";

    static readonly ShaderKeyword k_FramebufferFetch =
        new ShaderKeyword("UNITY_FRAMEBUFFER_FETCH_AVAILABLE");
    static readonly HashSet<string> s_loggedSnippets = new HashSet<string>();

    public int callbackOrder => 0;

    public void OnProcessShader(
        Shader shader,
        ShaderSnippetData snippet,
        IList<ShaderCompilerData> data)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            return;

        if (shader == null ||
            !string.Equals(shader.name, WebGLUnsafeShaderName, System.StringComparison.Ordinal))
        {
            StripFramebufferFetchVariants(shader, snippet, data);
            return;
        }

        LogSnippetOnce(shader, snippet, data.Count);

        if (!string.Equals(snippet.passName, WebGLAllowedPassName, System.StringComparison.Ordinal))
        {
            Debug.Log($"[WebGLFramebufferFetchStripper] Strip pass shader={shader.name} pass={snippet.passName} passType={snippet.passType} stage={snippet.shaderType} variants={data.Count}");
            data.Clear();
            return;
        }

        StripFramebufferFetchVariants(shader, snippet, data);
    }

    static void StripFramebufferFetchVariants(
        Shader shader,
        ShaderSnippetData snippet,
        IList<ShaderCompilerData> data)
    {
        int removed = 0;
        for (int i = data.Count - 1; i >= 0; i--)
        {
            if (data[i].shaderKeywordSet.IsEnabled(k_FramebufferFetch))
            {
                data.RemoveAt(i);
                removed++;
            }
        }

        if (removed > 0)
        {
            var shaderName = shader != null ? shader.name : "<null>";
            Debug.Log($"[WebGLFramebufferFetchStripper] Strip framebuffer-fetch variants shader={shaderName} pass={snippet.passName} removed={removed} remaining={data.Count}");
        }
    }

    static void LogSnippetOnce(Shader shader, ShaderSnippetData snippet, int variantCount)
    {
        string key = $"{shader.name}|{snippet.passName}|{snippet.passType}|{snippet.shaderType}";
        if (!s_loggedSnippets.Add(key))
            return;

        Debug.Log($"[WebGLFramebufferFetchStripper] Inspect shader={shader.name} pass={snippet.passName} passType={snippet.passType} stage={snippet.shaderType} variants={variantCount}");
    }
}
