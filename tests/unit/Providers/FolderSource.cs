namespace Microsoft.Learn.NoSQLValidation.UnitTests.Providers;

internal static class FolderSource
{
    public static TheoryData<string> TestData
    {
        get
        {
            List<string> directories = [];

            string? toolDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly()?.Location);
            if (toolDirectory is not null)
            {
                string? projectDirectory = Path.Combine(toolDirectory, "scripts");

                if (projectDirectory is not null)
                {
                    foreach (string directory in Directory.GetDirectories(projectDirectory))
                    {
                        string? output = Path.GetRelativePath(projectDirectory, directory) switch
                        {
                            ".devcontainer" or ".git" or ".vscode" => null,
                            "test" or "validate" => null,
                            _ => directory,
                        };
                        if (output is not null)
                        {
                            var name = new DirectoryInfo(output).Name;
                            directories.Add(name);
                        }
                    }
                }
            }

            TheoryData<string> result = [];
            foreach (string directory in directories)
            {
                result.Add(directory);
            }
            return result;
        }
    }
}