using System;

namespace STS2Mobile.Launcher;

public static class LegalNotices
{
    public static string Load()
    {
        try
        {
            var text = (string)LauncherModel.GetGodotApp()?.Call("readBundledLegalNotices");
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[Legal] notice load failed: {ex.Message}");
        }

        return Localization.Tr("LEGAL_LOAD_FAILED");
    }
}
