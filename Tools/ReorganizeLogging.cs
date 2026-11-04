#:property TargetFramework=net10.0

using System.Text.RegularExpressions;

// dotnet run --file Tools/ReorganizeLogging.cs -- .
string root = Path.GetFullPath(args[0]);
string codeDirectory = Path.Join(root, "Code");
string loggingDirectory = Path.Join(codeDirectory, "Lib", "AgentLib", "AgentLib", "Logging");

(string TypeName, string Folder)[] moves =
[
    ("FileCopilotChatSessionStore", "Sessions"),
    ("CopilotChatSessionPersistenceData", "Sessions"),
    ("CopilotChatSessionSummary", "Sessions"),
    ("SessionDirectoryStorage", "Sessions"),
    ("SessionResources", "Sessions"),
    ("SessionHistoryXmlCodec", "Sessions/Codecs"),
    ("ResponsesSessionJsonCodec", "Sessions/Codecs"),
    ("SessionStorageMigrations", "Sessions/Migrations"),
    ("LegacySessionXmlReader", "Sessions/Migrations"),
];

foreach (var (typeName, folder) in moves)
{
    string destinationDirectory = Path.Join(loggingDirectory, folder);
    Directory.CreateDirectory(destinationDirectory);
    string source = Path.Join(loggingDirectory, typeName + ".cs");
    string destination = Path.Join(destinationDirectory, typeName + ".cs");
    string targetNamespace = "AgentLib.Logging." + folder.Replace('/', '.');
    string content = File.ReadAllText(source)
        .Replace("namespace AgentLib.Logging;", $"namespace {targetNamespace};", StringComparison.Ordinal);
    File.Move(source, destination);
    File.WriteAllText(destination, content);
    Console.WriteLine($"{source} -> {destination}");
}

foreach (string file in EnumerateSourceFiles(codeDirectory))
{
    string original = File.ReadAllText(file);
    string content = original;
    var imports = new SortedSet<string>(StringComparer.Ordinal);

    foreach (var (typeName, folder) in moves)
    {
        string targetNamespace = "AgentLib.Logging." + folder.Replace('/', '.');
        content = content.Replace($"AgentLib.Logging.{typeName}", $"{targetNamespace}.{typeName}", StringComparison.Ordinal);

        if (Regex.IsMatch(content, $@"\b{typeName}\b")
            && !content.Contains($"namespace {targetNamespace};", StringComparison.Ordinal)
            && !content.Contains($"using {targetNamespace};", StringComparison.Ordinal))
        {
            imports.Add(targetNamespace);
        }
    }

    if (imports.Count > 0)
    {
        string newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        content = string.Join(newline, imports.Select(name => $"using {name};")) + newline + content;
    }

    if (content != original)
    {
        File.WriteAllText(file, content);
        Console.WriteLine(file);
    }
}

static IEnumerable<string> EnumerateSourceFiles(string directory)
{
    foreach (string file in Directory.EnumerateFiles(directory, "*.cs"))
    {
        yield return file;
    }

    foreach (string child in Directory.EnumerateDirectories(directory))
    {
        // 构建产物不参与源码重组。
        if (Path.GetFileName(child) is "bin" or "obj" or "artifacts" or "TestResults")
        {
            continue;
        }

        foreach (string file in EnumerateSourceFiles(child))
        {
            yield return file;
        }
    }
}
