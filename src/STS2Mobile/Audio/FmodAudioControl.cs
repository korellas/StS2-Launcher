using System;
using Godot;

namespace STS2Mobile.Audio;

// Backgrounding control for FMOD, which sits outside Godot's AudioServer and so
// ignores bus mutes entirely.
//
// The game's NAudioManager.SetMasterVol(0) only lowers a mixer volume: dumpsys
// shows FMOD's AAudio stream still in state:started and the music stays audible
// after the app leaves the foreground. Muting and pausing the events through the
// FMOD GDExtension singleton stops the output itself.
public static class FmodAudioControl
{
    private const string SingletonName = "FmodServer";

    public static void Suspend()
    {
        Invoke("mute_all_events");
        Invoke("pause_all_events");
        SetAllBusesMuted(true);
        Commit();
    }

    public static void Resume()
    {
        SetAllBusesMuted(false);
        Invoke("unpause_all_events");
        Invoke("unmute_all_events");
        Commit();
    }

    // FMOD Studio queues commands and only applies them on the next
    // Studio::System::update(), which the extension drives from the scene tree.
    // The caller pauses that tree immediately afterwards, so without an explicit
    // update the mute is accepted and never committed — the bug looked exactly
    // like the calls being ignored.
    private static void Commit() => Invoke("update");

    // mute_all_events only reaches events the extension is tracking, and it left
    // the music audible in practice. Muting every bus goes through FMOD's own
    // routing instead, so it covers whatever the game used to start playback.
    private static void SetAllBusesMuted(bool muted)
    {
        try
        {
            var server = GetServer();
            if (server == null || !server.HasMethod("get_all_buses"))
            {
                PatchHelper.Log("[Audio] get_all_buses unavailable, buses left alone");
                return;
            }

            var buses = server.Call("get_all_buses").AsGodotArray();
            int applied = 0;
            foreach (var entry in buses)
            {
                var bus = entry.AsGodotObject();
                if (bus == null || !bus.HasMethod("set_mute"))
                    continue;
                bus.Call("set_mute", muted);
                applied++;
            }

            PatchHelper.Log(
                $"[Audio] set_mute({muted}) applied to {applied}/{buses.Count} FMOD buses"
            );
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Audio] bus mute failed: {ex.Message}");
        }
    }

    private static GodotObject GetServer() =>
        Engine.HasSingleton(SingletonName) ? Engine.GetSingleton(SingletonName) : null;

    // Failures are logged rather than swallowed: if the extension ever renames
    // these, silence here would look exactly like the bug this code fixes.
    private static void Invoke(string method)
    {
        try
        {
            var server = GetServer();
            if (server == null || !server.HasMethod(method))
            {
                PatchHelper.Log($"[Audio] {SingletonName} has no method {method}");
                return;
            }

            server.Call(method);
            PatchHelper.Log($"[Audio] {method} ok");
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Audio] {method} failed: {ex.Message}");
        }
    }
}
