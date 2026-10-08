using System.Reflection;
using System.Reflection.Emit;
using STS2Mobile.Launcher;

void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}
var name = new AssemblyName("VersionFixture") { Version = new Version(2, 3, 4, 5) };
var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
assembly.DefineDynamicModule("Fixture");
var info = LibraryVersions.ReadLoaded("VersionFixture");
Check(info?.Version == "2.3.4.5", "Must read the loaded assembly version");
Check(Guid.TryParse(info.ModuleId, out _), "Must retain the complete module identity");
var attribute = typeof(AssemblyInformationalVersionAttribute).GetConstructor(
    new[] { typeof(string) }
);
assembly.SetCustomAttribute(
    new CustomAttributeBuilder(attribute, new object[] { "2.3.4-beta+commit" })
);
Check(
    LibraryVersions.ReadLoaded("VersionFixture").Version == "2.3.4-beta+commit",
    "Informational version must be preserved"
);
Check(
    LibraryVersions.ReadLoaded("NotLoadedFixture") == null,
    "Missing game library must not be loaded or initialized"
);

var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
try
{
    Check(LibraryVersions.ReadGameRelease(path) == null, "Missing release data is unavailable");
    File.WriteAllText(path, "{\"version\":\"v0.123.4\",\"commit\":\"abcdef12\"}");
    var release = LibraryVersions.ReadGameRelease(path);
    Check(
        release?.Version == "v0.123.4" && release.Commit == "abcdef12",
        "Game release metadata must remain distinct from DLL version"
    );
    File.WriteAllText(path, "{\"version\":42}");
    Check(
        LibraryVersions.ReadGameRelease(path) == null,
        "Invalid version type must be unavailable"
    );
    File.WriteAllText(path, "broken");
    Check(
        LibraryVersions.ReadGameRelease(path) == null,
        "Invalid release data must not block launcher startup"
    );
}
finally
{
    File.Delete(path);
}
Console.WriteLine(
    "PASS loaded library identity, game release metadata and absent/invalid startup data"
);
