using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace Melodica.Utility.Extensions;
public static partial class StringExtensions
{
    private static readonly char[] customIllegalChars =
    [
        '<'
    ];

    private static char[]? cachedIllegalChars;

    public static string SeperateStrings(this string[] strings, string seperator = ", ")
    {
        var sb = new StringBuilder(strings.Length * 5);
        for (var i = 0; i < strings.Length; i++)
        {
            sb.Append(strings[i]);
            if (i == strings.Length - 1)
                break;
            sb.Append(seperator);
        }
        return sb.ToString();
    }

    public static (string? artist, string songTitle) SeperateArtistName(this ReadOnlySpan<char> fullTitle)
    {
        int artistEndIndex = 0;
        int songStartIndex = 0;
        for (var i = 0; i < fullTitle.Length; i++)
        {
            char currentChar = fullTitle[i];
            switch (currentChar)
            {
                case (char)0x01C0: // latin letter dental click
                case (char)0xFF5C: // fullwidth vertical line
                case (char)0x275A: // heavy vertical bar
                case (char)0x2759: // medium vertical bar
                case (char)0x2758: // light vertical bar
                case '|':
                case '-' :
                    if (i > 0 && fullTitle[i - 1] == ' ' && fullTitle.Length > i + 1 && fullTitle[i + 1] == ' ')
                    {
                        artistEndIndex = i - 1;
                        songStartIndex = i + 2;
                    }
                    break;
            }
            if (artistEndIndex != 0)
                break;
        }

        if (artistEndIndex == 0)
            return (null, fullTitle.ToString());

        var artistName = fullTitle[..artistEndIndex];
        var songName = fullTitle[songStartIndex..];

        return (artistName.ToString(), songName.ToString());
    }

    public static string UrlFriendlyfy(this string input)
    {
        const string colon = "%3B";
        const string forwardSlash = "%2F";
        const string hash = "%23";
        const string questionmark = "%3F";
        const string ampersand = "%26";
        const string at = "%40";
        const string percentage = "%25";
        const string plus = "%2B";
        const string whitespace = "%20";
        StringBuilder sb = new(input.Length);
        foreach (var ch in input)
        {
            switch (ch)
            {
                case ':':
                    sb.Append(colon);
                    break;
                case '/':
                    sb.Append(forwardSlash);
                    break;
                case '#':
                    sb.Append(hash);
                    break;
                case '?':
                    sb.Append(questionmark);
                    break;
                case '&':
                    sb.Append(ampersand);
                    break;
                case '@':
                    sb.Append(at);
                    break;
                case '%':
                    sb.Append(percentage);
                    break;
                case '+':
                    sb.Append(plus);
                    break;
                case ' ':
                    sb.Append(whitespace);
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    public static string ReplaceIllegalCharacters(this string str, char replacer = '_')
    {
        cachedIllegalChars ??= Path.GetInvalidFileNameChars().Union(customIllegalChars).ToArray();

        var outStr = cachedIllegalChars.Aggregate(str, (current, c) => current.Replace(c, replacer));
        return outStr;
    }

    [GeneratedRegex(@"((http)|(https)):\/\/.+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UrlRegex();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUrl(this ReadOnlySpan<char> str) => UrlRegex().IsMatch(str);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUrl(this ReadOnlyMemory<char> str) => UrlRegex().IsMatch(str.Span);
}

