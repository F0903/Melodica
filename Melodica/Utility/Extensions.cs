using System.Text;
using System.Text.RegularExpressions;

using Discord;
using Discord.WebSocket;

using Melodica.Dependencies;

namespace Melodica.Utility.Extensions;

public static class Extensions
{
    private static readonly char[] customIllegalChars =
    {
            '<'
        };

    private static char[]? cachedIllegalChars;

    public static string ExtractFormatFromFileUrl(this ReadOnlySpan<char> url)
    {
        int dotIndex = url.LastIndexOf('.') + 1;
        return url[dotIndex..].ToString();
    }

    public static bool IsOverSize<T>(this IEnumerable<T> list, int exclusiveLimit)
    {
        int i = 0;
        foreach (T? item in list)
        {
            ++i;
            if (i > exclusiveLimit)
                return true;
        }
        return false;
    }

    public static string SeperateStrings(this string[] strings, string seperator = ", ")
    {
        StringBuilder? sb = new(strings.Length * 5);
        for (int i = 0; i < strings.Length; i++)
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
        int charIndx = songTitle.IndexOf('-');
        int spaceIndx;
        bool containsSeperator = charIndx != -1;
        int endIndx = containsSeperator ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;

        bool useBackup = endIndx == songTitle.Length;
        string? artist = useBackup ? backupArtistName : songTitle[0..endIndx].ToString();
        int titleOffset = endIndx + (containsSeperator ? 3 : 1);
        string? title = useBackup ? songTitle.ToString() : songTitle[titleOffset..songTitle.Length].ToString();
        return (artist, title);
    }

    public static string ExtractArtistName(this ReadOnlySpan<char> songTitle)
    {
        int charIndx = songTitle.IndexOf('-');
        int spaceIndx;
        int endIndx = charIndx != -1 ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;
        return songTitle[0..endIndx].ToString();
    }

    public static string FixURLWhitespace(this string input, string whitespaceReplacement = "%20")
    {
        return input.Replace(" ", whitespaceReplacement);
    }

    public static TimeSpan Sum<T>(this IEnumerable<T> input, Func<T, TimeSpan> selector)
    {
        TimeSpan sum = new();
        foreach (T? item in input)
            sum += selector(item);
        return sum;
    }

    public static async Task<TimeSpan> SumAsync<T>(this IEnumerable<T> input, Func<T, Task<TimeSpan>> selector)
    {
        TimeSpan sum = new();
        foreach (T? item in input)
            sum += await selector(item);
        return sum;
    }

    public static IEnumerable<To> Convert<From, To>(this IEnumerable<From> col, Func<From, To> body)
    {
        foreach (From? elem in col)
            yield return body(elem);
    }

    public static bool CheckForUser(this SocketGuild guild, string user)
    {
        return AutoGetUser(guild, user) != null;
    }

    public static SocketGuildUser? AutoGetUser(this SocketGuild guild, string user)
    {
        return guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);
    }

    public static bool IsOwnerOfApp(this IUser user)
    {
        return user.Id == Dependency.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;
    }

    public static string ReplaceIllegalCharacters(this string str, char replacer = '_')
    {
        cachedIllegalChars ??= Path.GetInvalidFileNameChars().Union(customIllegalChars).ToArray();

        string outStr = cachedIllegalChars.Aggregate(str, (current, c) => current.Replace(c, replacer));
        return outStr;
    }

    static readonly Regex urlRegex = new(@"((http)|(https)):\/\/.+", RegexOptions.Compiled);

    public static bool IsUrl(this ReadOnlySpan<char> str)
    {
        return urlRegex.IsMatch(str.ToString());
    }

    public static bool IsUrl(this ReadOnlyMemory<char> str)
    {
        return urlRegex.IsMatch(str.ToString());
    }

    public static bool IsUrl(this string str)
    {
        return urlRegex.IsMatch(str);
    }
}
