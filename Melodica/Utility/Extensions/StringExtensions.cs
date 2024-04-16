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

    public static (string artist, string newTitle) SeperateArtistName(this ReadOnlySpan<char> songTitle, string backupArtistName = "Unknown Artist")
    {
        var seperatorIndex = songTitle.IndexOf(" - ");
        int spaceIndx;
        var containsSeperator = seperatorIndex != -1;
        var endIndx = containsSeperator ? seperatorIndex : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;

        var useBackup = endIndx == songTitle.Length;
        var artist = useBackup ? backupArtistName : songTitle[0..endIndx].ToString();
        var titleOffset = endIndx + (containsSeperator ? 3 : 1);
        var title = useBackup ? songTitle.ToString() : songTitle[titleOffset..songTitle.Length].ToString();
        return (artist, title);
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

