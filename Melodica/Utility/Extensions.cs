using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Melodica.Dependencies;

namespace Melodica.Utility;

public static partial class Extensions
{
    private static readonly char[] customIllegalChars =
    [
        '<'
    ];

    private static char[]? cachedIllegalChars;

    public static string ExtractFormatFromFileUrl(this ReadOnlySpan<char> url)
    {
        var dotIndex = url.LastIndexOf('.') + 1;
        return url[dotIndex..].ToString();
    }

    public static bool IsOverSize<T>(this IEnumerable<T> list, int exclusiveLimit)
    {
        var i = 0;
        foreach (var item in list)
        {
            ++i;
            if (i > exclusiveLimit)
                return true;
        }
        return false;
    }

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
        var charIndx = songTitle.IndexOf('-');
        int spaceIndx;
        var containsSeperator = charIndx != -1;
        var endIndx = containsSeperator ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;

        var useBackup = endIndx == songTitle.Length;
        var artist = useBackup ? backupArtistName : songTitle[0..endIndx].ToString();
        var titleOffset = endIndx + (containsSeperator ? 3 : 1);
        var title = useBackup ? songTitle.ToString() : songTitle[titleOffset..songTitle.Length].ToString();
        return (artist, title);
    }

    public static string ExtractArtistName(this ReadOnlySpan<char> songTitle)
    {
        var charIndx = songTitle.IndexOf('-');
        int spaceIndx;
        var endIndx = charIndx != -1 ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;
        return songTitle[0..endIndx].ToString();
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

    public static TimeSpan Sum<T>(this IEnumerable<T> input, Func<T, TimeSpan> selector)
    {
        TimeSpan sum = new();
        foreach (var item in input)
            sum += selector(item);
        return sum;
    }

    public static async Task<TimeSpan> SumAsync<T>(this IEnumerable<T> input, Func<T, Task<TimeSpan>> selector)
    {
        TimeSpan sum = new();
        foreach (var item in input)
            sum += await selector(item);
        return sum;
    }

    public static IEnumerable<To> Convert<From, To>(this IEnumerable<From> col, Func<From, To> body)
    {
        foreach (var elem in col)
            yield return body(elem);
    }

    public static bool CheckForUser(this SocketGuild guild, string user) => guild.AutoGetUser(user) != null;

    public static SocketGuildUser? AutoGetUser(this SocketGuild guild, string user) => guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);

    public static bool IsOwnerOfApp(this IUser user) => user.Id == Dependency.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

    public static string ReplaceIllegalCharacters(this string str, char replacer = '_')
    {
        cachedIllegalChars ??= Path.GetInvalidFileNameChars().Union(customIllegalChars).ToArray();

        var outStr = cachedIllegalChars.Aggregate(str, (current, c) => current.Replace(c, replacer));
        return outStr;
    }

    [GeneratedRegex(@"((http)|(https)):\/\/.+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UrlRegex();

    public static bool IsUrl(this ReadOnlySpan<char> str) => UrlRegex().IsMatch(str.ToString());

    public static bool IsUrl(this ReadOnlyMemory<char> str) => UrlRegex().IsMatch(str.ToString());

    public static bool IsUrl(this string str) => UrlRegex().IsMatch(str);
}
