#:property TargetFramework=net10.0

// dotnet run --file Tools/ReorganizeAgentLibCoding.cs -- .
string projectDirectory = Path.Join(args[0], "Code", "Lib", "AgentLib", "AgentLib.Coding");

(string FileName, string Folder)[] moves =
[
    ("CodingAgent.cs", "ChatClient"),
    ("ChatClientCodingAgentRunResult.cs", "ChatClient"),
    ("CodingAgentOptions.cs", "Runtime"),
    ("CodingChatRunOptions.cs", "Runtime"),
    ("CodingRunContext.cs", "Runtime"),
    ("CodingToolContext.cs", "Tools"),
    ("ICodingAgentRunResult.cs", "Runtime"),
    ("CompletedCodingAgentRunResult.cs", "Runtime"),
    ("CodingCompressionThresholds.cs", "Compression"),
    ("CompressionToolCallObserver.cs", "Compression"),
    ("CodingPromptProvider.cs", "Prompts"),
    ("CodingSystemPrompt.cs", "Prompts"),
    ("CodingSubAgentSessions.cs", "SubAgents"),
    ("CodingSubAgentTools.cs", "Tools/SubAgents"),
    ("CodingModelPurposes.cs", "SubAgents"),
    ("CodingWorkspaceCache.cs", "Runtime"),
    ("CodingWorkspaceContentTools.cs", "Tools/Workspace"),
    ("ICodingWorkspaceToolSource.cs", "Tools"),
    ("WorkspaceProjectCatalog.cs", "Tools/Workspace"),
    ("DotNetApiTools.cs", "Tools/DotNet"),
    ("DotNetCliProcess.cs", "Tools/DotNet"),
    ("DotNetCliTools.cs", "Tools/DotNet"),
    ("RoslynAgentTools.cs", "Tools/Roslyn"),
    ("RoslynLspClient.cs", "Tools/Roslyn"),
    ("DeepSeekWebToolSource.cs", "Tools/Web"),
];

foreach (var (fileName, folder) in moves)
{
    string destinationDirectory = Path.Join(projectDirectory, folder);
    Directory.CreateDirectory(destinationDirectory);
    File.Move(Path.Join(projectDirectory, fileName), Path.Join(destinationDirectory, fileName));
}

string toolsDirectory = Path.Join(projectDirectory, "Tools");
string[] toolFolders = ["ExecuteCommands", "Images", "Sandboxes"];

foreach (string folder in toolFolders)
{
    Directory.Move(Path.Join(projectDirectory, folder), Path.Join(toolsDirectory, folder));
}
