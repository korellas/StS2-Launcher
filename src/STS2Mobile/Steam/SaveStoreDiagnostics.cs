using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace STS2Mobile.Steam;

// Reports what the game's save-store interfaces currently require.
//
// SteamKit2CloudSaveStore implements interfaces owned by sts2.dll, so a game
// update that adds a member makes the whole type unloadable — the launcher hit
// exactly that this morning, when HasUserEnabledCloudSync appeared and every
// cloud operation started failing with a type-load error that named no member.
// Printing the interface members turns the next occurrence into a one-line diff
// instead of another investigation.
public static class SaveStoreDiagnostics
{
    public static string DescribeInterfaces()
    {
        try
        {
            var assembly = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly;
            var sb = new StringBuilder();

            foreach (var name in new[]
            {
                "MegaCrit.Sts2.Core.Saves.ICloudSaveStore",
                "MegaCrit.Sts2.Core.Saves.ISaveStore",
            })
            {
                var type = assembly.GetType(name);
                if (type == null)
                {
                    sb.Append(name).Append(": missing from the game assembly. ");
                    continue;
                }

                var members = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Select(m => $"{m.Name}({m.GetParameters().Length})")
                    .OrderBy(x => x, StringComparer.Ordinal);

                sb.Append(type.Name)
                    .Append(" [")
                    .Append(string.Join(", ", members))
                    .Append("] ");
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"interface inspection failed: {ex.Message}";
        }
    }
}
