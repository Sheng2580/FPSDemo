using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CombatFeedbackMaterialTools
{
    private const string ParticleUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";
    private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

    private enum CombatFeedbackBlendMode
    {
        Alpha,
        Additive
    }

    private struct MaterialFixConfig
    {
        public string path;
        public bool useParticleShader;
        public CombatFeedbackBlendMode blendMode;

        public MaterialFixConfig(string path, bool useParticleShader, CombatFeedbackBlendMode blendMode)
        {
            this.path = path;
            this.useParticleShader = useParticleShader;
            this.blendMode = blendMode;
        }
    }

    private static readonly MaterialFixConfig[] MaterialFixConfigs =
    {
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Muzzle Flash.mat", true, CombatFeedbackBlendMode.Additive),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Muzzle Smoke.mat", true, CombatFeedbackBlendMode.Alpha),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Blood.mat", true, CombatFeedbackBlendMode.Alpha),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Wood.mat", true, CombatFeedbackBlendMode.Alpha),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Stone Smoke.mat", true, CombatFeedbackBlendMode.Alpha),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Sparks.mat", true, CombatFeedbackBlendMode.Additive),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Stone Decal.mat", false, CombatFeedbackBlendMode.Alpha),
        new MaterialFixConfig("Assets/Art/Animation/Akila/FPS Framework/Art/Materials/Decal Heat.mat", false, CombatFeedbackBlendMode.Additive)
    };

    [MenuItem("FPSDemo/Build/修复战斗特效材质", priority = 4)]
    public static void FixCombatFeedbackMaterialsMenu()
    {
        FixCombatFeedbackMaterials(true);
    }

    public static bool FixCombatFeedbackMaterials(bool logResult)
    {
        Shader particleShader = Shader.Find(ParticleUnlitShaderName);
        Shader unlitShader = Shader.Find(UnlitShaderName);
        if (particleShader == null || unlitShader == null)
        {
            Debug.LogError($"[CombatFeedbackMaterialTools] 找不到 URP Shader Particle={particleShader != null} Unlit={unlitShader != null}");
            return false;
        }

        bool success = true;
        int fixedCount = 0;
        for (int i = 0; i < MaterialFixConfigs.Length; i++)
        {
            MaterialFixConfig config = MaterialFixConfigs[i];
            Material material = AssetDatabase.LoadAssetAtPath<Material>(config.path);
            if (material == null)
            {
                success = false;
                Debug.LogError($"[CombatFeedbackMaterialTools] 找不到特效材质 {config.path}");
                continue;
            }

            Shader targetShader = config.useParticleShader ? particleShader : unlitShader;
            if (FixMaterial(material, targetShader, config.blendMode))
            {
                fixedCount++;
                EditorUtility.SetDirty(material);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logResult)
        {
            Debug.Log(success
                ? $"[CombatFeedbackMaterialTools] 战斗特效材质修复完成 Count={fixedCount}"
                : "[CombatFeedbackMaterialTools] 战斗特效材质修复存在失败项");
        }

        return success;
    }

    private static bool FixMaterial(Material material, Shader targetShader, CombatFeedbackBlendMode blendMode)
    {
        Texture mainTexture = FindTexture(material);
        Color baseColor = FindColor(material);
        Color emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
        bool changed = material.shader != targetShader;

        if (changed)
        {
            material.shader = targetShader;
        }

        changed |= SetTexture(material, "_BaseMap", mainTexture);
        changed |= SetTexture(material, "_MainTex", mainTexture);
        changed |= SetColor(material, "_BaseColor", baseColor);
        changed |= SetColor(material, "_Color", baseColor);
        changed |= SetColor(material, "_EmissionColor", emissionColor);
        changed |= SetTransparentBlend(material, blendMode);
        return changed;
    }

    private static Texture FindTexture(Material material)
    {
        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
        {
            return material.GetTexture("_BaseMap");
        }

        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
        {
            return material.GetTexture("_MainTex");
        }

        if (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null)
        {
            return material.GetTexture("_EmissionMap");
        }

        return null;
    }

    private static Color FindColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        return Color.white;
    }

    private static bool SetTransparentBlend(Material material, CombatFeedbackBlendMode blendMode)
    {
        bool changed = false;
        changed |= SetFloat(material, "_Surface", 1f);
        changed |= SetFloat(material, "_Blend", blendMode == CombatFeedbackBlendMode.Additive ? 2f : 0f);
        changed |= SetFloat(material, "_Cull", (float)CullMode.Back);
        changed |= SetFloat(material, "_AlphaClip", 0f);
        changed |= SetFloat(material, "_BlendOp", (float)BlendOp.Add);
        changed |= SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        changed |= SetFloat(material, "_DstBlend", blendMode == CombatFeedbackBlendMode.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        changed |= SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
        changed |= SetFloat(material, "_DstBlendAlpha", blendMode == CombatFeedbackBlendMode.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        changed |= SetFloat(material, "_ZWrite", 0f);
        changed |= SetFloat(material, "_SoftParticlesEnabled", 0f);
        changed |= SetFloat(material, "_CameraFadingEnabled", 0f);

        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");

        if (blendMode == CombatFeedbackBlendMode.Additive)
        {
            material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        return changed;
    }

    private static bool SetTexture(Material material, string propertyName, Texture texture)
    {
        if (!material.HasProperty(propertyName))
        {
            return false;
        }

        if (material.GetTexture(propertyName) == texture)
        {
            return false;
        }

        material.SetTexture(propertyName, texture);
        return true;
    }

    private static bool SetColor(Material material, string propertyName, Color color)
    {
        if (!material.HasProperty(propertyName))
        {
            return false;
        }

        if (material.GetColor(propertyName) == color)
        {
            return false;
        }

        material.SetColor(propertyName, color);
        return true;
    }

    private static bool SetFloat(Material material, string propertyName, float value)
    {
        if (!material.HasProperty(propertyName))
        {
            return false;
        }

        if (Mathf.Approximately(material.GetFloat(propertyName), value))
        {
            return false;
        }

        material.SetFloat(propertyName, value);
        return true;
    }
}
