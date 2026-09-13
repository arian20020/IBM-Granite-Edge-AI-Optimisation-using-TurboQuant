using System;
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using FontStyle = Windows.UI.Text.FontStyle;

namespace GraniteEdgeAI.Features.GgufRuntime.Presentation;

/// <summary>
/// Bounded, inert Markdown presentation, not a full CommonMark parser.
/// Unsupported syntax stays text. No HTML, URI activation, images or external content.
/// </summary>
internal static class ChatMarkdownRenderer
{
    internal static void Render(RichTextBlock target, string markdown)
    {
        target.Blocks.Clear();
        string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        char fence = '\0';
        int fenceLength = 0;
        var code = new StringBuilder();
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            string trimmed = line.TrimStart();
            if (fence != '\0')
            {
                int count = LeadingCount(trimmed, fence);
                if (count >= fenceLength && trimmed.AsSpan(count).Trim().IsEmpty)
                {
                    AddCode(target, code.ToString().TrimEnd('\n'));
                    code.Clear();
                    fence = '\0';
                }
                else code.Append(line).Append('\n');
                continue;
            }
            if (target.Blocks.Count >= 1024)
            {
                // Bound native object count without dropping the remaining response.
                var remainder = new Paragraph();
                remainder.Inlines.Add(new Run { Text = string.Join("\n", lines, index, lines.Length - index) });
                target.Blocks.Add(remainder);
                break;
            }
            if (trimmed.StartsWith("```", StringComparison.Ordinal) ||
                trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                fence = trimmed[0];
                fenceLength = LeadingCount(trimmed, fence);
                continue;
            }
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            int heading = LeadingCount(trimmed, '#');
            if (heading is >= 1 and <= 6 && trimmed.Length > heading && trimmed[heading] == ' ')
            {
                paragraph.FontSize = heading == 1 ? 24 : heading == 2 ? 22 : heading == 3 ? 20 : 16;
                paragraph.FontWeight = FontWeights.SemiBold;
                line = trimmed[(heading + 1)..];
            }
            else if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                paragraph.Inlines.Add(new Run { Text = "│ " });
                line = trimmed[2..];
            }
            else if (trimmed.Length >= 2 && trimmed[1] == ' ' && trimmed[0] is '-' or '+' or '*')
            {
                paragraph.Inlines.Add(new Run { Text = "• " });
                line = trimmed[2..];
            }
            else
            {
                int digits = 0;
                while (digits < trimmed.Length && digits < 9 && char.IsAsciiDigit(trimmed[digits])) digits++;
                if (digits > 0 && trimmed.Length > digits + 1 &&
                    trimmed[digits] is '.' or ')' && trimmed[digits + 1] == ' ')
                {
                    paragraph.Inlines.Add(new Run { Text = trimmed[..(digits + 2)] });
                    line = trimmed[(digits + 2)..];
                }
            }
            AddInlines(paragraph.Inlines, line, 0);
            target.Blocks.Add(paragraph);
        }
        if (fence != '\0') AddCode(target, code.ToString().TrimEnd('\n'));
    }

    private static int LeadingCount(string text, char marker)
    {
        int count = 0;
        while (count < text.Length && text[count] == marker) count++;
        return count;
    }

    private static void AddCode(RichTextBlock target, string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 4, 0, 8), FontFamily = new FontFamily("Consolas") };
        paragraph.Inlines.Add(new Run { Text = text });
        target.Blocks.Add(paragraph);
    }

    private static void AddInlines(InlineCollection output, string text, int depth)
    {
        if (depth >= 4)
        {
            output.Add(new Run { Text = text });
            return;
        }
        var plain = new StringBuilder();
        void Flush()
        {
            if (plain.Length == 0) return;
            output.Add(new Run { Text = plain.ToString() });
            plain.Clear();
        }
        for (int index = 0; index < text.Length;)
        {
            if (output.Count >= 256)
            {
                plain.Append(text.AsSpan(index));
                break;
            }
            char marker = text[index];
            if (marker == '\\' && index + 1 < text.Length && "\\`*_{}[]()#+-.!>".Contains(text[index + 1]))
            {
                plain.Append(text[index + 1]);
                index += 2;
                continue;
            }
            if (marker is not ('*' or '_' or '`') ||
                marker == '_' && index > 0 && char.IsLetterOrDigit(text[index - 1]))
            {
                plain.Append(marker);
                index++;
                continue;
            }
            int count = 1;
            while (index + count < text.Length && text[index + count] == marker) count++;
            if (count > 3)
            {
                plain.Append(text.AsSpan(index, count));
                index += count;
                continue;
            }
            string delimiter = new(marker, count);
            int end = text.IndexOf(delimiter, index + count, StringComparison.Ordinal);
            if (end <= index + count)
            {
                // an incomplete streaming delimiter remains readable verbatim
                plain.Append(text.AsSpan(index));
                break;
            }
            Flush();
            string content = text.Substring(index + count, end - index - count);
            if (marker == '`')
                output.Add(new Run { Text = content, FontFamily = new FontFamily("Consolas") });
            else
            {
                var span = new Span();
                if (count >= 2) span.FontWeight = FontWeights.SemiBold;
                if (count != 2) span.FontStyle = FontStyle.Italic;
                AddInlines(span.Inlines, content, depth + 1);
                output.Add(span);
            }
            index = end + count;
        }
        Flush();
    }
}
