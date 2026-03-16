using UnityEngine;
using UnityEditor;

public class CharacterMoodTool : EditorWindow
{
    // ── Values the artist controls ──
    private Color _shadowTint = new Color(0.20f, 0.20f, 0.40f, 1f);
    private Color _highlightTint = new Color(1.00f, 0.95f, 0.80f, 1f);
    private float _saturation = 1f;
    private float _pulseSpeed = 1f;
    private float _emissionMultiplier = 1f;
    private int _toonSteps = 4;
    private float _fresnelIntensity = 0f;
    private Color _rimColour = Color.white;
    private float _specularHardness = 0f;
    private bool _toolEnabled = false;

    [MenuItem("Tools/Character Mood Tool")]
    public static void Open()
    {
        GetWindow<CharacterMoodTool>("Character Mood Tool");
    }

    void OnEnable()
    {
        ApplyToScene();
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        _toolEnabled = EditorGUILayout.Toggle("Enable Tool", _toolEnabled);
        if (EditorGUI.EndChangeCheck()) ApplyToScene();
        EditorGUILayout.Space(5);

        GUILayout.Label("Colour Palette", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _shadowTint = EditorGUILayout.ColorField("Shadow Tint", _shadowTint);
        _highlightTint = EditorGUILayout.ColorField("Highlight Tint", _highlightTint);
        _saturation = EditorGUILayout.Slider("Saturation", _saturation, 0f, 2f);
        if (EditorGUI.EndChangeCheck()) ApplyToScene();

        EditorGUILayout.Space(10);
        GUILayout.Label("Emissives", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _pulseSpeed = EditorGUILayout.Slider("Pulse Speed", _pulseSpeed, 0f, 5f);
        _emissionMultiplier = EditorGUILayout.Slider("Emission Multiplier", _emissionMultiplier, 0f, 5f);
        if (EditorGUI.EndChangeCheck()) ApplyToScene();

        EditorGUILayout.Space(10);
        GUILayout.Label("Stylisation", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _toonSteps = EditorGUILayout.IntSlider("Toon Steps", _toonSteps, 2, 8);
        _fresnelIntensity = EditorGUILayout.Slider("Rim Light Intensity", _fresnelIntensity, 0f, 1f);
        _rimColour = EditorGUILayout.ColorField("Rim Light Colour", _rimColour);
        _specularHardness = EditorGUILayout.Slider("Specular Hardness", _specularHardness, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) ApplyToScene();

        EditorGUILayout.Space(15);
        if (GUILayout.Button("Bake to Build"))
        {
            BakeToDefaults();
        }
    }

    void ApplyToScene()
    {
        if (!_toolEnabled)
        {
            Shader.SetGlobalColor("_Char_ShadowTint", Color.white);
            Shader.SetGlobalColor("_Char_HighlightTint", Color.white);
            Shader.SetGlobalFloat("_Char_Saturation", 1f);
            Shader.SetGlobalFloat("_Char_PulseSpeed", 0f);
            Shader.SetGlobalFloat("_Char_EmissionMultiplier", 1f);
            Shader.SetGlobalInt("_Char_ToonSteps", 8);
            Shader.SetGlobalFloat("_Char_FresnelIntensity", 0f);
            Shader.SetGlobalColor("_Char_RimColour", Color.black);
            Shader.SetGlobalFloat("_Char_SpecularHardness", 1f);
            return;
        }

        Shader.SetGlobalColor("_Char_ShadowTint", _shadowTint);
        Shader.SetGlobalColor("_Char_HighlightTint", _highlightTint);
        Shader.SetGlobalFloat("_Char_Saturation", _saturation);
        Shader.SetGlobalFloat("_Char_PulseSpeed", _pulseSpeed);
        Shader.SetGlobalFloat("_Char_EmissionMultiplier", _emissionMultiplier);
        Shader.SetGlobalInt("_Char_ToonSteps", _toonSteps);
        Shader.SetGlobalFloat("_Char_FresnelIntensity", _fresnelIntensity);
        Shader.SetGlobalColor("_Char_RimColour", _rimColour);
        Shader.SetGlobalFloat("_Char_SpecularHardness", _specularHardness);
    }

    void BakeToDefaults()
    {
        string path = "Assets/Maddie/Scripts/MoodShaderDefaults.cs";

        string content =
            @"using UnityEngine;

public class MoodShaderDefaults : MonoBehaviour
{
    void Awake()
    {
        // Character globals - baked from CharacterMoodTool
        Shader.SetGlobalColor(""_Char_ShadowTint"",         " + ColorToCode(_shadowTint) + @");
        Shader.SetGlobalColor(""_Char_HighlightTint"",      " + ColorToCode(_highlightTint) + @");
        Shader.SetGlobalFloat(""_Char_Saturation"",         " + _saturation + @"f);
        Shader.SetGlobalFloat(""_Char_PulseSpeed"",         " + _pulseSpeed + @"f);
        Shader.SetGlobalFloat(""_Char_EmissionMultiplier"", " + _emissionMultiplier + @"f);
        Shader.SetGlobalInt  (""_Char_ToonSteps"",          " + _toonSteps + @");
        Shader.SetGlobalFloat(""_Char_FresnelIntensity"",   " + _fresnelIntensity + @"f);
        Shader.SetGlobalColor(""_Char_RimColour"",          " + ColorToCode(_rimColour) + @");
        Shader.SetGlobalFloat(""_Char_SpecularHardness"",   " + _specularHardness + @"f);

        // Environment globals - neutral until EnvironmentMoodTool is baked
        Shader.SetGlobalColor(""_Env_ShadowTint"",         new Color(1f, 1f, 1f, 1f));
        Shader.SetGlobalColor(""_Env_HighlightTint"",      new Color(1f, 1f, 1f, 1f));
        Shader.SetGlobalFloat(""_Env_Saturation"",         1f);
        Shader.SetGlobalFloat(""_Env_PulseSpeed"",         0f);
        Shader.SetGlobalFloat(""_Env_EmissionMultiplier"", 1f);
        Shader.SetGlobalInt  (""_Env_ToonSteps"",          8);
        Shader.SetGlobalFloat(""_Env_FresnelIntensity"",   0f);
        Shader.SetGlobalColor(""_Env_RimColour"",          new Color(0f, 0f, 0f, 1f));
        Shader.SetGlobalFloat(""_Env_SpecularHardness"",   1f);
    }
}";

        System.IO.File.WriteAllText(path, content);
        AssetDatabase.Refresh();
        Debug.Log("[MoodTool] Character values baked to MoodShaderDefaults.cs");
    }

    string ColorToCode(Color c)
    {
        return $"new Color({c.r}f, {c.g}f, {c.b}f, {c.a}f)";
    }
}