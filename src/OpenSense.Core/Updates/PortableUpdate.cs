namespace OpenSense.Core.Updates;

/// <summary>
/// The file work of a portable update: the new version's files go over the old copy, and when one can't be copied
/// (e.g. it is still in use), the old copy is put back as it was.
/// </summary>
public static class PortableUpdate
{
    /// <summary>
    /// Copies every file of <paramref name="source"/> into <paramref name="target"/>, keeping each file it replaces in
    /// <paramref name="backup"/>. Files in <paramref name="target"/> that <paramref name="source"/> lacks stay.
    /// </summary>
    /// <returns>How many files were replaced and how many added.</returns>
    /// <exception cref="IOException">A file could not be copied; <paramref name="target"/> holds the old copy again.</exception>
    /// <exception cref="UnauthorizedAccessException">As <see cref="IOException"/>.</exception>
    public static (int Replaced, int Added) CopyOver(string source, string target, string backup)
    {
        var replaced = new List<string>();
        var added = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination))
                {
                    var saved = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Copy(destination, saved, overwrite: true);
                    replaced.Add(relative);
                }
                else
                {
                    added.Add(relative);
                }
                File.Copy(file, destination, overwrite: true);
            }
            return (replaced.Count, added.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File by file: the one that failed may still be in use, and the others go back all the same.
            foreach (var relative in replaced)
                Undo(() => File.Copy(Path.Combine(backup, relative), Path.Combine(target, relative), overwrite: true));
            foreach (var relative in added)
                Undo(() => File.Delete(Path.Combine(target, relative)));
            throw;
        }
    }

    private static void Undo(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // stays as it is
        }
    }
}
