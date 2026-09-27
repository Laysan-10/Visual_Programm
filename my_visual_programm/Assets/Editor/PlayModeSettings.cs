using UnityEditor;

[InitializeOnLoad]
static class PlayModeSettings
{
    static PlayModeSettings()
    {
        EditorSettings.enterPlayModeOptionsEnabled = false;
    }
}
