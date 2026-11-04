using System;
using System.IO;

namespace PrismCodingAgent.CodingShell.Infrastructure;

internal sealed record CodingChatStartupOptions(bool IsOnboardingDebug, CodingChatRoomPaths Paths)
{
    internal static CodingChatStartupOptions FromArguments(string[]? arguments)
    {
        bool isOnboardingDebug = arguments?.Contains("--debug-onboarding", StringComparer.OrdinalIgnoreCase) == true;
        if (isOnboardingDebug)
        {
            return new CodingChatStartupOptions(true,
                CodingChatRoomPaths.Create(Path.Join(Path.GetTempPath(), "CodingChatRoom-OnboardingDebug", Guid.NewGuid().ToString("N"))));
        }
        int directoryIndex = Array.FindIndex(arguments ?? [], argument =>
            string.Equals(argument, "--data-directory", StringComparison.OrdinalIgnoreCase));
        if (directoryIndex >= 0)
        {
            if (arguments is null || directoryIndex + 1 >= arguments.Length
                || string.IsNullOrWhiteSpace(arguments[directoryIndex + 1])
                || arguments[directoryIndex + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("--data-directory");
            }
            return new CodingChatStartupOptions(isOnboardingDebug, CodingChatRoomPaths.Create(arguments[directoryIndex + 1]));
        }
        return new CodingChatStartupOptions(false, CodingChatRoomPaths.CreateForCurrentUser());
    }
}
