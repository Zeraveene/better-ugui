using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Coffee.UIEffects.Editors
{
    internal class RemoveSoftMaskableVariantsIfNeeded : IPreprocessShaders
    {
        public int callbackOrder => 1;

        // Remove the 'SOFTMASKABLE' variants if 'SoftMaskForUGUI' package is not installed.
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // If the shader is not for UIEffect: skip.
            if (!shader.name.EndsWith(" (UIEffect)")) return;

            // Recognize the bundled module before checking standalone package installs.
            if (AssetDatabase.LoadAssetAtPath<MonoScript>("Packages/com.zeraveene.better-ugui/ThirdParty/SoftMaskForUGUI/Runtime/SoftMask.cs") != null) return;

            // If the 'SoftMaskForUGUI' package is installed: skip.
            if (PackageInfo.FindForAssetPath("Packages/com.zeraveene.better-ugui/ThirdParty/SoftMaskForUGUI/package.json") != null) return;

            // Remove the 'SOFTMASKABLE' variants.
            var softMaskable = new ShaderKeyword(shader, "SOFTMASKABLE");
            for (var i = data.Count - 1; i >= 0; i--)
            {
                var d = data[i];
                if (d.shaderKeywordSet.IsEnabled(softMaskable))
                {
                    data.RemoveAt(i);
                }
            }
        }
    }
}
