namespace OpenSense.Core.Tests;

/// <summary>The repository the tests were built from, for tests that check files in it.</summary>
internal static class SourceTree
{
    public static string Root { get; } = FindRoot();

    /// <summary>The test project's folder, where its fixture files are kept.</summary>
    public static string Tests { get; } = Path.Combine(Root, "tests", "OpenSense.Core.Tests");

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "OpenSense.sln")))
                return folder.FullName;
        }
        throw new DirectoryNotFoundException("OpenSense.sln not found above " + AppContext.BaseDirectory);
    }
}
