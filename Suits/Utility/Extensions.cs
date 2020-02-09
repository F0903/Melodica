using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Utility.Extensions
{
    public static class Extensions
    {
        public static char[] CustomIllegalChars { get; set; } =
        {
            
        };

        public static string Unfold<T>(this IEnumerable<T> str, char? seperatorChar = null)
        {
            if (str.Count() == 0)
                return str.ElementAtOrDefault(0)?.ToString();
            var sb = new StringBuilder();
            foreach (var item in str)
            {
                sb.Append(item.ToString() + seperatorChar.GetValueOrDefault() + ' ');
            }
            var removeNum = seperatorChar != null ? 2 : 1;
            sb.Remove(sb.Length - removeNum, removeNum);
            return sb.ToString();
        }

        public static byte[] ToBytes(this Stream stream, uint bufferSize = 16 * 1024)
        {
            byte[] buffer = new byte[bufferSize];
            using var mem = new MemoryStream();
            int count = 0;
            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0) 
            {
                mem.Write(buffer, 0, count);
            }
            return mem.ToArray();
        }

        public static TimeSpan Sum<T>(this IEnumerable<T> input, Func<T, TimeSpan> selector)
        {
            TimeSpan sum = new TimeSpan();
            Parallel.ForEach(input, x =>
            {
                sum = sum.Add(selector(x));
            });
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
    }
}