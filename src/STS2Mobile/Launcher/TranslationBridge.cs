using System;
using Godot;

namespace STS2Mobile.Launcher;

// Thin wrapper over the platform translation service exposed by GodotApp.
//
// The Java side runs the request asynchronously and parks the outcome in a
// field; this polls it. A callback would need a JNI bridge back into Godot,
// which is a lot of machinery for one request at a time.
public sealed class TranslationBridge
{
    public enum State
    {
        Idle,
        Running,
        Done,
        Failed,
    }

    private readonly GodotObject _godotApp;

    public TranslationBridge()
    {
        _godotApp = LauncherModel.GetGodotApp();
    }

    public bool Available => _godotApp != null;

    // Logged once at startup: an unsupported device should be diagnosable from
    // the console rather than looking like the feature silently doing nothing.
    public string Capabilities()
    {
        try
        {
            return (string)_godotApp?.Call("translationCapabilities") ?? "unavailable: no bridge";
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    public bool Start(string text, string from = "en", string to = "ko")
    {
        try
        {
            _godotApp?.Call("startTranslation", text, from, to);
            return true;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Translate] start failed: {ex.Message}");
            return false;
        }
    }

    public State Poll()
    {
        try
        {
            return (string)_godotApp?.Call("getTranslationState") switch
            {
                "running" => State.Running,
                "done" => State.Done,
                "failed" => State.Failed,
                _ => State.Idle,
            };
        }
        catch
        {
            return State.Failed;
        }
    }

    public string Result()
    {
        try
        {
            return (string)_godotApp?.Call("getTranslationResult") ?? "";
        }
        catch
        {
            return "";
        }
    }
}
