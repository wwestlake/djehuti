using System.Text.RegularExpressions;

namespace Djehuti.DjeLab.Services;

/// <summary>
/// Rewrites \[ \] / \( \) LaTeX delimiters to $$ $$ / $ $ (the forms that
/// survive Markdown parsing intact -- see DjeLabSystemPrompt.cs), and the
/// standalone align/align* environment to aligned (the only form KaTeX
/// supports nested inside $$ $$). Used in two places: AiChatClient normalizes
/// a reply once when it first arrives, and ChatPane normalizes again at
/// render time -- belt and suspenders, and the render-time pass also
/// retroactively fixes any turn already sitting in saved chat history from
/// before this existed.
/// </summary>
public static class MathDelimiterNormalizer
{
    private static readonly Regex DisplayMathPattern = new(@"\\\[(.+?)\\\]", RegexOptions.Singleline);
    private static readonly Regex InlineMathPattern = new(@"\\\((.+?)\\\)", RegexOptions.Singleline);
    private static readonly Regex AlignEnvironmentPattern = new(@"\\(begin|end)\{align\*?\}");

    public static string Normalize(string content)
    {
        content = DisplayMathPattern.Replace(content, m => $"$${m.Groups[1].Value}$$");
        content = InlineMathPattern.Replace(content, m => $"${m.Groups[1].Value}$");
        content = AlignEnvironmentPattern.Replace(content, m => $"\\{m.Groups[1].Value}{{aligned}}");
        return content;
    }
}
