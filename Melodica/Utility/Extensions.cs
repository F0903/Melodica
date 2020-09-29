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
        public static char[] CustomIllegalChars { get; set; } =
        {
            '<'
        };

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

        public static (string artist, string newTitle) SeperateArtistName(this string songTitle, string backupTitle = " ")
        {
            int charIndx = songTitle.IndexOf('-');
            int spaceIndx;
            int endIndx = charIndx != -1 ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;
            return (songTitle[0..endIndx], endIndx != songTitle.Length ? songTitle[(endIndx + (charIndx != -1 ? 3 : 1))..songTitle.Length] : backupTitle);
        }

        public static string ExtractArtistName(this string songTitle)
        {
            int charIndx = songTitle.IndexOf('-');
            int spaceIndx;
            int endIndx = charIndx != -1 ? charIndx - 1 : (spaceIndx = songTitle.IndexOf(' ')) != -1 ? spaceIndx : songTitle.Length;
            return songTitle[0..endIndx];
        }

        public static string FixURLWhitespace(this string input, string whitespaceReplacement = "%20") => input.Replace(" ", whitespaceReplacement);

        public static async Task<TimeSpan> GetTotalDurationAsync(this YoutubeExplode.Playlists.Playlist pl, YoutubeExplode.YoutubeClient? client = null)
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
            client ??= new YoutubeExplode.YoutubeClient();
            var video = (await client.Playlists.GetVideosAsync(pl.Id).BufferAsync(1)).First();
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

        public static SocketGuildUser AutoGetUser(this SocketGuild guild, string user) =>
            guild.Users.SingleOrDefault(x => x.Username == user || x.Nickname == user || x.Id.ToString() == user);

        public static bool IsOwnerOfApp(this IUser user) =>
            user.Id == IoC.Kernel.Get<DiscordSocketClient>().GetApplicationInfoAsync().Result.Owner.Id;

        public static string ReplaceIllegalCharacters(this string str, string replacement = "_")
        {
            string? newStr = Path.GetInvalidFileNameChars().Union(CustomIllegalChars).Aggregate(str, (current, c) => current.Replace(c.ToString(), replacement));
            if (newStr[^1] == '.')
                newStr.Remove(newStr.Length - 1, 1);
            return newStr;
        }

        public static bool IsUrl(this string str) =>
             Uri.TryCreate(str, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps);

        public static bool LikeYouTubeId(this string str)
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