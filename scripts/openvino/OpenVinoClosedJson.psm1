Set-StrictMode -Version Latest

if (-not ('OpenVinoClosedJson.StrictParser' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenVinoClosedJson
{
    public sealed class StrictParser
    {
        private readonly string text;
        private readonly int maximumDepth;
        private int position;

        private StrictParser(string text, int maximumDepth)
        {
            this.text = text;
            this.maximumDepth = maximumDepth;
        }

        public static void Validate(string text, int maximumDepth)
        {
            if (text == null || maximumDepth < 1)
                throw new FormatException("Invalid parser input.");
            StrictParser parser = new StrictParser(text, maximumDepth);
            parser.SkipWhitespace();
            parser.ReadValue(1);
            parser.SkipWhitespace();
            if (parser.position != text.Length)
                throw new FormatException("Trailing JSON content.");
        }

        private void ReadValue(int depth)
        {
            if (depth > maximumDepth || position >= text.Length)
                throw new FormatException("JSON depth or value is invalid.");
            switch (text[position])
            {
                case '{': ReadObject(depth); return;
                case '[': ReadArray(depth); return;
                case '"': ReadString(); return;
                case 't': ReadLiteral("true"); return;
                case 'f': ReadLiteral("false"); return;
                case 'n': ReadLiteral("null"); return;
                default: ReadNumber(); return;
            }
        }

        private void ReadObject(int depth)
        {
            position++;
            SkipWhitespace();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            if (Take('}')) return;
            while (true)
            {
                if (position >= text.Length || text[position] != '"')
                    throw new FormatException("Object property name is missing.");
                string name = ReadString();
                if (!names.Add(name))
                    throw new FormatException("Duplicate decoded property name.");
                SkipWhitespace();
                Require(':');
                SkipWhitespace();
                ReadValue(depth + 1);
                SkipWhitespace();
                if (Take('}')) return;
                Require(',');
                SkipWhitespace();
            }
        }

        private void ReadArray(int depth)
        {
            position++;
            SkipWhitespace();
            if (Take(']')) return;
            while (true)
            {
                ReadValue(depth + 1);
                SkipWhitespace();
                if (Take(']')) return;
                Require(',');
                SkipWhitespace();
            }
        }

        private string ReadString()
        {
            Require('"');
            StringBuilder value = new StringBuilder();
            while (position < text.Length)
            {
                char current = text[position++];
                if (current == '"') return value.ToString();
                if (current < 0x20)
                    throw new FormatException("Unescaped control character.");
                if (current == '\\')
                {
                    if (position >= text.Length)
                        throw new FormatException("Incomplete escape.");
                    char escape = text[position++];
                    switch (escape)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u': AppendEscapedCodeUnit(value); break;
                        default: throw new FormatException("Unknown escape.");
                    }
                }
                else if (char.IsHighSurrogate(current))
                {
                    if (position >= text.Length || !char.IsLowSurrogate(text[position]))
                        throw new FormatException("Unpaired high surrogate.");
                    value.Append(current);
                    value.Append(text[position++]);
                }
                else if (char.IsLowSurrogate(current))
                {
                    throw new FormatException("Unpaired low surrogate.");
                }
                else
                {
                    value.Append(current);
                }
            }
            throw new FormatException("Unterminated string.");
        }

        private void AppendEscapedCodeUnit(StringBuilder value)
        {
            char first = ReadHexCodeUnit();
            if (char.IsHighSurrogate(first))
            {
                if (position + 2 > text.Length || text[position] != '\\' ||
                    text[position + 1] != 'u')
                    throw new FormatException("Unpaired escaped high surrogate.");
                position += 2;
                char second = ReadHexCodeUnit();
                if (!char.IsLowSurrogate(second))
                    throw new FormatException("Invalid escaped surrogate pair.");
                value.Append(first);
                value.Append(second);
            }
            else if (char.IsLowSurrogate(first))
            {
                throw new FormatException("Unpaired escaped low surrogate.");
            }
            else
            {
                value.Append(first);
            }
        }

        private char ReadHexCodeUnit()
        {
            if (position + 4 > text.Length)
                throw new FormatException("Incomplete Unicode escape.");
            int code = 0;
            for (int index = 0; index < 4; index++)
            {
                char digit = text[position++];
                int value = digit >= '0' && digit <= '9' ? digit - '0' :
                    digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 :
                    digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                if (value < 0) throw new FormatException("Invalid Unicode escape.");
                code = (code << 4) | value;
            }
            return (char)code;
        }

        private void ReadNumber()
        {
            int start = position;
            Take('-');
            if (Take('0'))
            {
                if (position < text.Length && char.IsDigit(text[position]))
                    throw new FormatException("Leading zero in number.");
            }
            else
            {
                if (position >= text.Length || text[position] < '1' || text[position] > '9')
                    throw new FormatException("Invalid number.");
                while (position < text.Length && char.IsDigit(text[position])) position++;
            }
            if (Take('.'))
            {
                int digits = position;
                while (position < text.Length && char.IsDigit(text[position])) position++;
                if (digits == position) throw new FormatException("Invalid fraction.");
            }
            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                position++;
                if (position < text.Length && (text[position] == '+' || text[position] == '-')) position++;
                int digits = position;
                while (position < text.Length && char.IsDigit(text[position])) position++;
                if (digits == position) throw new FormatException("Invalid exponent.");
            }
            if (start == position) throw new FormatException("Invalid JSON token.");
        }

        private void ReadLiteral(string literal)
        {
            if (position + literal.Length > text.Length ||
                string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
                throw new FormatException("Invalid literal.");
            position += literal.Length;
        }

        private void SkipWhitespace()
        {
            while (position < text.Length &&
                (text[position] == ' ' || text[position] == '\t' ||
                 text[position] == '\r' || text[position] == '\n')) position++;
        }

        private bool Take(char expected)
        {
            if (position < text.Length && text[position] == expected)
            {
                position++;
                return true;
            }
            return false;
        }

        private void Require(char expected)
        {
            if (!Take(expected)) throw new FormatException("Expected JSON punctuation.");
        }
    }
}
'@
}

function Get-OpenVinoClosedJsonText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidateRange(1, 1048576)][int]$MaximumBytes,
        [Parameter(Mandatory)][ValidateRange(1, 64)][int]$MaximumDepth
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw 'closed-json-file-missing'
    }
    $file = Get-Item -LiteralPath $fullPath -Force
    if ($file.Length -le 0 -or $file.Length -gt $MaximumBytes -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'closed-json-file-invalid'
    }
    $strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
    $raw = $strictUtf8.GetString([IO.File]::ReadAllBytes($fullPath))
    if ($raw.Length -gt 0 -and $raw[0] -eq [char]0xFEFF) {
        throw 'closed-json-bom-invalid'
    }
    [OpenVinoClosedJson.StrictParser]::Validate($raw, $MaximumDepth)
    return $raw
}

Export-ModuleMember -Function Get-OpenVinoClosedJsonText
