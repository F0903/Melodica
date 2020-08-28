using AngleSharp.Text;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Melodica.Utility.Extensions
{
    public static class Extensions
    {
        public static char[] CustomIllegalChars { get; set; } =
        {
            '<'
        };

        public static string FixURLWhitespace(this string input)
        {
            return input.Replace(" ", "%20");
        }

        public async static Task<TimeSpan> GetTotalDurationAsync(this YoutubeExplode.Playlists.Playlist pl, YoutubeExplode.YoutubeClient? client = null) 
        {
            client ??= new YoutubeExplode.YoutubeClient();
            var videos = client.Playlists.GetVideosAsync(pl.Id);
            TimeSpan ts = new TimeSpan();
            await foreach (var video in videos)
            {
                ts += video.Duration;
            }
            return ts;
        }

        public async static Task<string> GetPlaylistThumbnail(this YoutubeExplode.Playlists.Playlist pl, YoutubeExplode.YoutubeClient? client = null)
        {
            client ??= new YoutubeExplode.YoutubeClient();
            var video = (await client.Playlists.GetVideosAsync(pl.Id).BufferAsync(1)).First();
            return video.Thumbnails.MediumResUrl;
        }

        public static string Unfold<T>(this IEnumerable<T> str, char? seperatorChar = null)
        {
            if (str.Count() == 0)
                return str.ElementAtOrDefault(0)?.ToString()!;
            var sb = new StringBuilder();
            foreach (var item in str)
            {
                sb.Append(item!.ToString() + seperatorChar.GetValueOrDefault() + ' ');
            }
            var removeNum = seperatorChar != null ? 2 : 1;
            sb.Remove(sb.Length - removeNum, removeNum);
            return sb.ToString();
        }

        public static TimeSpan Sum<T>(this IEnumerable<T> input, Func<T, TimeSpan> selector)
        {
            TimeSpan sum = new TimeSpan();
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
            var newStr = Path.GetInvalidFileNameChars().Union(CustomIllegalChars).Aggregate(str, (current, c) => current.Replace(c.ToString(), replacement));
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
            foreach (var letter in str)
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