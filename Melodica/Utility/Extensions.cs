using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AngleSharp.Text;

using Discord;
using Discord.WebSocket;

using YoutubeExplode;

namespace Melodica.Utility.Extensions
{
    public static class Extensions
    {
        private static readonly char[] customIllegalChars =
        {
            '<'
        };

        private static char[]? cachedIllegalChars;

        public static bool IsOverSize<T>(this IEnumerable<T> list, int exclusiveLimit)
        {
            int i = 0;
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
            for (int i = 0; i < strings.Length; i++)
            {
                sb.Append(strings[i]);
                if (i == strings.Length - 1)
                    break;
                sb.Append(seperator);
            }
            return sb.ToString();
        }

        public static (string artist, string newTitle) SeperateArtistName(this ReadOnlySpan<char> songTitle, string backupTitle = " ")
        {
            int charIndx = songTitle.IndexOf('-');
            int spaceIndx;
            int endIndx = charIndx != -1 ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;
            return (songTitle[0..endIndx].ToString(), endIndx != songTitle.Length ? songTitle[(endIndx + (charIndx != -1 ? 3 : 1))..songTitle.Length].ToString() : backupTitle);
        }

        public static string ExtractArtistName(this ReadOnlySpan<char> songTitle)
        {
            int charIndx = songTitle.IndexOf('-');
            int spaceIndx;
            int endIndx = charIndx != -1 ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;
            return songTitle[0..endIndx].ToString();
        }

        public static string FixURLWhitespace(this string input, string whitespaceReplacement = "%20") => input.Replace(" ", whitespaceReplacement);

        public static async Task<TimeSpan> GetTotalDurationAsync(this YoutubeExplode.Playlists.Playlist pl, YoutubeClient? client = null)
        {
            client ??= new YoutubeExplode.YoutubeClient();
            var videos = client.Playlists.GetVideosAsync(pl.Id);
            var ts = new TimeSpan();
            await foreach (var video in videos)
            {
                ts += video.Duration;
            }
            return ts;
        }

        public static async Task<string> GetPlaylistThumbnail(this YoutubeExplode.Playlists.Playlist pl, YoutubeExplode.YoutubeClient? client = null)
        {
            client ??= new YoutubeClient();
            var video = (await client.Playlists.GetVideosAsync(pl.Id).BufferAsync(1))[0];
            return video.Thumbnails.MediumResUrl;
        }

        public static TimeSpan Sum<T>(this IEnumerable<T> input, Func<T, TimeSpan> selector)
        {
            var sum = new TimeSpan();
            foreach (var item in input)
                sum += selector(item);
            return sum;
        }

        public static IEnumerable<To> Convert<From, To>(this IEnumerable<From> col, Func<From, To> body)
        {
            foreach (var elem in col)
                yield return body(elem);
        }

        public static bool CheckForUser(this SocketGuild guild, string user) =>
            AutoGetUser(guild, user) != null;

        public static SocketGuildUser? AutoGetUser(this SocketGuild guild, string user) =>
            guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);

        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

        public static string ReplaceIllegalCharacters(this string str, char replacer = '_')
        {
            cachedIllegalChars ??= Path.GetInvalidFileNameChars().Union(customIllegalChars).ToArray();

            string outStr = cachedIllegalChars.Aggregate(str, (current, c) => current.Replace(c, replacer));
            return outStr;
        }

        public static bool IsUrl(this string str) =>
             Uri.TryCreate(str, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps);

        public static bool LikeYouTubeId(this ReadOnlySpan<char> str)
        {
            if (str.Length > 34)
                return false;

            int numOfSmall = 0;
            int numOfLarge = 0;
            foreach (char letter in str)
            {
                if (letter == ' ')
                    return false;
                if (letter.IsLowercaseAscii()) numOfSmall++;
                else numOfLarge++;
            }
            return numOfSmall > 3 && numOfLarge > 2; // Might need tweaking over time.
        }
    }
}