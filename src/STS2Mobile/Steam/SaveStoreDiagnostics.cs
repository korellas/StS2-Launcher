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

            AppendIdentity(sb, assembly);
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"interface inspection failed: {ex.Message}";
        }
    }

    // Every required member is present, so the failure is not interface drift.
    // "Invalid type X for instance field" is what Mono reports when two types
    // share a name but not an identity — the same assembly loaded twice, or a
    // build compiled against a different sts2.dll than the device runs. These
    // three lines separate those cases; nothing else can tell them apart.
    private static void AppendIdentity(StringBuilder sb, Assembly gameAssembly)
    {
        try
        {
            var ours = typeof(SteamKit2CloudSaveStore);
            var runtimeIface = gameAssembly.GetType("MegaCrit.Sts2.Core.Saves.ICloudSaveStore");
            var compiledIface = typeof(MegaCrit.Sts2.Core.Saves.ICloudSaveStore);

            sb.Append(" | assignable=")
                .Append(runtimeIface?.IsAssignableFrom(ours) == true ? "yes" : "NO")
                .Append(" sameIface=")
                .Append(ReferenceEquals(runtimeIface, compiledIface) ? "yes" : "NO");

            sb.Append(" | game=").Append(Describe(gameAssembly));
            sb.Append(" mod=").Append(Describe(ours.Assembly));

            var duplicates = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name is "STS2Mobile" or "sts2")
                .Select(Describe)
                .ToArray();
            if (duplicates.Length > 2)
                sb.Append(" | DUPLICATES: ").Append(string.Join(" ; ", duplicates));
        }
        catch (Exception ex)
        {
            sb.Append(" | identity check failed: ").Append(ex.Message);
        }
    }

    private static string Describe(Assembly assembly)
    {
        var name = assembly.GetName();
        var location = string.IsNullOrEmpty(assembly.Location)
            ? "<no location>"
            : System.IO.Path.GetFileName(assembly.Location);
        return $"{name.Name} v{name.Version} mvid={assembly.ManifestModule.ModuleVersionId:N} @{location}";
    }
}
