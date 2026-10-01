namespace Cluster.View;

internal static class SharedKeyRing
{
    /// <summary>
    /// Resolves a single directory shared by every app in the solution so the data protection
    /// key ring is persisted outside of each project's bin folder.
    /// </summary>
    public static string Resolve(string contentRootPath)
    {
        var directory = new DirectoryInfo(contentRootPath);

        while (directory is not null)
        {
            if (directory.EnumerateFiles("cluster-sln.sln").Any()
                || directory.EnumerateDirectories().Any(d => d.Name == "src"))
            {
                return Path.Combine(directory.FullName, ".keys");
            }

            directory = directory.Parent;
        }

        return Path.Combine(contentRootPath, ".keys");
    }
}