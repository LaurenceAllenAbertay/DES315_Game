using UnityEngine;

public class MoodShaderDefaults : MonoBehaviour
{
    void Awake()
    {
        // Character globals - baked from CharacterMoodTool
        Shader.SetGlobalColor("_Char_ShadowTint",         new Color(0f, 0f, 1f, 1f));
        Shader.SetGlobalColor("_Char_HighlightTint",      new Color(1f, 0f, 0.4925146f, 1f));
        Shader.SetGlobalFloat("_Char_Saturation",         1f);
        Shader.SetGlobalFloat("_Char_PulseSpeed",         1f);
        Shader.SetGlobalFloat("_Char_EmissionMultiplier", 1f);
        Shader.SetGlobalInt  ("_Char_ToonSteps",          4);
        Shader.SetGlobalFloat("_Char_FresnelIntensity",   0f);
        Shader.SetGlobalColor("_Char_RimColour",          new Color(1f, 1f, 1f, 1f));
        Shader.SetGlobalFloat("_Char_SpecularHardness",   0f);

        // Environment globals - neutral until EnvironmentMoodTool is baked
        Shader.SetGlobalColor("_Env_ShadowTint",         new Color(1f, 1f, 1f, 1f));
        Shader.SetGlobalColor("_Env_HighlightTint",      new Color(1f, 1f, 1f, 1f));
        Shader.SetGlobalFloat("_Env_Saturation",         1f);
        Shader.SetGlobalFloat("_Env_PulseSpeed",         0f);
        Shader.SetGlobalFloat("_Env_EmissionMultiplier", 1f);
        Shader.SetGlobalInt  ("_Env_ToonSteps",          8);
        Shader.SetGlobalFloat("_Env_FresnelIntensity",   0f);
        Shader.SetGlobalColor("_Env_RimColour",          new Color(0f, 0f, 0f, 1f));
        Shader.SetGlobalFloat("_Env_SpecularHardness",   1f);
    }
}