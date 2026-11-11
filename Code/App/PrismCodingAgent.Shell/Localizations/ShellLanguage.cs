using System;
using System.Globalization;
using System.Linq;

namespace PrismCodingAgent.CodingShell.Localizations;

internal static class ShellLanguage
{
    internal static string Normalize(string? languageTag) => Lang.SupportedLanguageTags
        .FirstOrDefault(tag => string.Equals(tag, languageTag, StringComparison.OrdinalIgnoreCase)) ?? "zh-Hans";

    internal static string GetDefault()
    {
        for (var culture = CultureInfo.CurrentUICulture; culture.Name.Length > 0; culture = culture.Parent)
        {
            var supported = Lang.SupportedLanguageTags.FirstOrDefault(tag =>
                string.Equals(tag, culture.Name, StringComparison.OrdinalIgnoreCase));
            if (supported is not null)
            {
                return supported;
            }
        }

        return "zh-Hans";
    }
}
