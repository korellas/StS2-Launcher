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

    // The previous version of this asked for typeof(SteamKit2CloudSaveStore),
    // which is the very type whose load fails — so the whole report died with
    // "VTable setup failed" before printing anything. Nothing here touches that
    // type directly; the load is attempted by name, last, with its own guard, so
    // a failure there still leaves the identity lines on screen.
    private static void AppendIdentity(StringBuilder sb, Assembly gameAssembly)
    {
        var ours = typeof(SaveStoreDiagnostics).Assembly;

        Try(sb, "assemblies", () =>
            $"game={Describe(gameAssembly)} mod={Describe(ours)}");

        Try(sb, "duplicates", () =>
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name is "STS2Mobile" or "sts2")
                .Select(Describe)
                .ToArray();
            return loaded.Length > 2 ? string.Join(" ; ", loaded) : $"none ({loaded.Length} loaded)";
        });

        Try(sb, "iface", () =>
        {
            var runtime = gameAssembly.GetType("MegaCrit.Sts2.Core.Saves.ICloudSaveStore");
            var compiled = typeof(MegaCrit.Sts2.Core.Saves.ICloudSaveStore);
            return ReferenceEquals(runtime, compiled)
                ? "same instance"
                : $"DIFFERENT: runtime={Describe(runtime?.Assembly)} compiled={Describe(compiled.Assembly)}";
        });

        // Last, and by name: this is the call that is expected to throw. Mono
        // names the member it could not resolve in the TypeLoadException, which
        // is the one piece of information the member listing above cannot give.
        Try(sb, "load", () =>
        {
            ours.GetType("STS2Mobile.Steam.SteamKit2CloudSaveStore", throwOnError: true);
            return "ok";
        });
    }

    private static void Try(StringBuilder sb, string label, Func<string> probe)
    {
        sb.Append(" | ").Append(label).Append('=');
        try
        {
            sb.Append(probe());
        }
        catch (Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
                sb.Append('[').Append(e.GetType().Name).Append(": ").Append(e.Message).Append(']');
        }
    }

    private static string Describe(Assembly assembly)
    {
        if (assembly == null)
            return "<null>";
        var name = assembly.GetName();
        return $"{name.Name} v{name.Version} mvid={assembly.ManifestModule.ModuleVersionId:N}";
    }
}
