#:property TargetFramework=net10.0
#:property PublishAot=false

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

// dotnet run --file Tools/ReorganizeShell.cs -- C:\path\to\PrismCodingAgent
// 不传参数时，使用此源文件所在仓库。此工具仅执行一次。
string repository = args.Length == 0
    ? Path.GetDirectoryName(Path.GetDirectoryName(GetSourcePath()))!
    : args[0];
string appDirectory = Path.Combine(repository, "Code", "App");
string avaloniaDirectory = Path.Combine(appDirectory, "PrismCodingAgent.AvaloniaShell");
string wpfDirectory = Path.Combine(appDirectory, "PrismCodingAgent.WpfShell");
string shellDirectory = Path.Combine(appDirectory, "PrismCodingAgent.Shell");

// 两个项目现在共享目录，普通 C# 文件只保留一份；不修改任何命名空间。
Directory.Move(avaloniaDirectory, shellDirectory);
foreach (string source in Directory.GetFiles(wpfDirectory))
{
    string name = Path.GetFileName(source);
    string destinationName = name == "AssemblyInfo.cs" ? "AssemblyInfo.wpf.cs" : name;
    File.Move(source, Path.Combine(shellDirectory, destinationName));
}
Directory.Delete(wpfDirectory, recursive: true);

string[] avaloniaFiles =
[
    "Program.cs",
    "Converters/BoolConverters.cs",
    "Converters/WorkspaceTitleConverter.cs",
    "Infrastructure/AvaloniaMainThreadDispatcher.cs",
    "Views/BinaryImage.cs",
    "Views/ChatEntryTemplateSelector.cs",
    "Views/ChatMessageItemTemplateSelector.cs",
];
foreach (string relativePath in avaloniaFiles)
{
    string source = Path.Combine(shellDirectory, relativePath);
    File.Move(source, Path.ChangeExtension(source, ".ava.cs"));
}

string avaloniaProject = Path.Combine(shellDirectory, "PrismCodingAgent.AvaloniaShell.csproj");
string wpfProject = Path.Combine(shellDirectory, "PrismCodingAgent.WpfShell.csproj");
ConfigureProject(avaloniaProject, "Avalonia");
ConfigureProject(wpfProject, "Wpf");

// 仅替换路径中的目录段，保留项目名称、测试名称和命名空间。
string[] referenceFiles =
[
    "PrismCodingAgent.slnx",
    "Code/Test/PrismCodingAgent.AvaloniaShell.Tests/PrismCodingAgent.AvaloniaShell.Tests.csproj",
    "README.md",
    "README.en.md",
];
foreach (string relativePath in referenceFiles)
{
    string file = Path.Combine(repository, relativePath);
    string content = File.ReadAllText(file);
    foreach (string oldDirectory in new[] { "PrismCodingAgent.AvaloniaShell", "PrismCodingAgent.WpfShell" })
    {
        content = content
            .Replace($"App/{oldDirectory}/", "App/PrismCodingAgent.Shell/", StringComparison.Ordinal)
            .Replace($"App\\{oldDirectory}\\", "App\\PrismCodingAgent.Shell\\", StringComparison.Ordinal);
    }
    File.WriteAllText(file, content);
}

Console.WriteLine(shellDirectory);

static void ConfigureProject(string path, string framework)
{
    XDocument project = XDocument.Load(path);
    XElement root = project.Root!;
    root.Elements("PropertyGroup").First().Add(new XElement("UseUIFramework", framework));

    // 显式排除另一套界面和代码，规则仅作用于这两个 Shell 项目。
    string[] excludedCode = framework == "Avalonia"
        ? ["**/*.wpf.cs", "**/*.xaml.cs"]
        : ["**/*.ava.cs", "**/*.axaml.cs"];
    string excludedMarkup = framework == "Avalonia" ? "**/*.xaml" : "**/*.axaml";
    root.Add(new XElement("ItemGroup",
        excludedCode.Select(pattern => new XElement("Compile", new XAttribute("Remove", pattern))),
        new XElement("None", new XAttribute("Remove", excludedMarkup)),
        new XElement("Page", new XAttribute("Remove", excludedMarkup)),
        new XElement("ApplicationDefinition", new XAttribute("Remove", excludedMarkup)),
        new XElement("AvaloniaResource", new XAttribute("Remove", excludedMarkup))));
    project.Save(path);
}

static string GetSourcePath([CallerFilePath] string path = "") => path;
