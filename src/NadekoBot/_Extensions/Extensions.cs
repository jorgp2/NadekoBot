﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ImageSharp;
using NadekoBot.Services.Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NadekoBot.Extensions
{
    public static class Extensions
    {
        private const string arrow_left = "⬅";
        private const string arrow_right = "➡";
        
        /// <summary>
        /// danny kamisama
        /// </summary>
        public static async Task SendPaginatedConfirmAsync(this IMessageChannel channel, int currentPage, Func<int, EmbedBuilder> pageFunc, int? lastPage = null, bool addPaginatedFooter = true)
        {
            lastPage += 1;
            var embed = pageFunc(currentPage);

            if(addPaginatedFooter)
                embed.AddPaginatedFooter(currentPage, lastPage);

            var msg = await channel.EmbedAsync(embed) as IUserMessage;

            if (currentPage >= lastPage && lastPage == 1)
                return;

            await msg.AddReactionAsync(arrow_left).ConfigureAwait(false);
            await msg.AddReactionAsync(arrow_right).ConfigureAwait(false);

            await Task.Delay(2000).ConfigureAwait(false);

            Action<SocketReaction> changePage = async r =>
            {
                try
                {
                    if (r.Emoji.Name == arrow_left)
                    {
                        if (currentPage == 1)
                            return;
                        var toSend = pageFunc(--currentPage);
                        if (addPaginatedFooter)
                            toSend.AddPaginatedFooter(currentPage, lastPage);
                        await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                    }
                    else if (r.Emoji.Name == arrow_right)
                    {
                        if (lastPage == null || lastPage > currentPage)
                        {
                            var toSend = pageFunc(++currentPage);
                            if (addPaginatedFooter)
                                toSend.AddPaginatedFooter(currentPage, lastPage);
                            await msg.ModifyAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            };

            using (msg.OnReaction(changePage, changePage))
            {
                await Task.Delay(30000).ConfigureAwait(false);
            }

            await msg.RemoveAllReactionsAsync().ConfigureAwait(false);
        }

        private static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
        {
            if (lastPage != null)
                return embed.WithFooter(efb => efb.WithText($"page {curPage} / {lastPage}"));
            else
                return embed.WithFooter(efb => efb.WithText($"page {curPage}"));
        }

        public static ReactionEventWrapper OnReaction(this IUserMessage msg, Action<SocketReaction> reactionAdded, Action<SocketReaction> reactionRemoved = null)
        {
            if (reactionRemoved == null)
                reactionRemoved = delegate { };

            var wrap = new ReactionEventWrapper(msg);
            wrap.OnReactionAdded += reactionAdded;
            wrap.OnReactionRemoved += reactionRemoved;
            return wrap;
        }

        public static void AddFakeHeaders(this HttpClient http)
        {
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.1 (KHTML, like Gecko) Chrome/14.0.835.202 Safari/535.1");
            http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        }

        public static DateTime ToUnixTimestamp(this double number) => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(number);

        public static EmbedBuilder WithOkColor(this EmbedBuilder eb) =>
            eb.WithColor(NadekoBot.OkColor);

        public static EmbedBuilder WithErrorColor(this EmbedBuilder eb) =>
            eb.WithColor(NadekoBot.ErrorColor);

        public static IMessage DeleteAfter(this IUserMessage msg, int seconds)
        {
            Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000);
                try { await msg.DeleteAsync().ConfigureAwait(false); }
                catch { }
            });
            return msg;
        }

        public static string GetPrefix(this ModuleInfo module) => NadekoBot.ModulePrefixes[module.GetTopLevelModule().Name];

        public static ModuleInfo GetTopLevelModule(this ModuleInfo module)
        {
            while (module.Parent != null)
            {
                module = module.Parent;
            }
            return module;
        }

        public static async Task<IMessage> SendMessageToOwnerAsync(this IGuild guild, string message)
        {
            var ownerPrivate = await (await guild.GetOwnerAsync().ConfigureAwait(false)).CreateDMChannelAsync()
                                .ConfigureAwait(false);

            return await ownerPrivate.SendMessageAsync(message).ConfigureAwait(false);
        }

        //public static async Task<IEnumerable<IGuildUser>> MentionedUsers(this IUserMessage msg) =>


        public static IEnumerable<IRole> GetRoles(this IGuildUser user) =>
            user.RoleIds.Select(r => user.Guild.GetRole(r)).Where(r => r != null);

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> elems, Action<T> exec)
        {
            foreach (var elem in elems)
            {
                exec(elem);
            }
            return elems;
        }

        public static void AddRange<T>(this HashSet<T> target, IEnumerable<T> elements) where T : class
        {
            foreach (var item in elements)
            {
                target.Add(item);
            }
        }

        public static void AddRange<T>(this ConcurrentHashSet<T> target, IEnumerable<T> elements) where T : class
        {
            foreach (var item in elements)
            {
                target.Add(item);
            }
        }

        public static bool IsInteger(this decimal number) => number == Math.Truncate(number);

        public static string SanitizeMentions(this string str) =>
            str.Replace("@everyone", "@everyοne").Replace("@here", "@һere");

        public static double UnixTimestamp(this DateTime dt) => dt.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        public static async Task<IUserMessage> SendMessageAsync(this IUser user, string message, bool isTTS = false) =>
            await (await user.CreateDMChannelAsync().ConfigureAwait(false)).SendMessageAsync(message, isTTS).ConfigureAwait(false);

        public static async Task<IUserMessage> SendConfirmAsync(this IUser user, string text)
             => await (await user.CreateDMChannelAsync()).SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(text));

        public static async Task<IUserMessage> SendConfirmAsync(this IUser user, string title, string text, string url = null)
             => await (await user.CreateDMChannelAsync()).SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(text)
                 .WithTitle(title).WithUrl(url));

        public static async Task<IUserMessage> SendErrorAsync(this IUser user, string title, string error, string url = null)
             => await (await user.CreateDMChannelAsync()).SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(error)
                 .WithTitle(title).WithUrl(url));

        public static async Task<IUserMessage> SendErrorAsync(this IUser user, string error)
             => await (await user.CreateDMChannelAsync()).SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(error));

        public static async Task<IUserMessage> SendFileAsync(this IUser user, string filePath, string caption = null, string text = null, bool isTTS = false) =>
            await (await user.CreateDMChannelAsync().ConfigureAwait(false)).SendFileAsync(File.Open(filePath, FileMode.Open), caption ?? "x", text, isTTS).ConfigureAwait(false);

        public static async Task<IUserMessage> SendFileAsync(this IUser user, Stream fileStream, string fileName, string caption = null, bool isTTS = false) =>
            await (await user.CreateDMChannelAsync().ConfigureAwait(false)).SendFileAsync(fileStream, fileName, caption, isTTS).ConfigureAwait(false);

        public static bool IsAuthor(this IUserMessage msg) =>
            NadekoBot.Client.CurrentUser.Id == msg.Author.Id;

        public static IEnumerable<IUser> Members(this IRole role) =>
            role.Guild.GetUsersAsync().GetAwaiter().GetResult().Where(u => u.RoleIds.Contains(role.Id)) ?? Enumerable.Empty<IUser>();

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel ch, EmbedBuilder embed, string msg = "")
             => ch.SendMessageAsync(msg, embed: embed);

        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string title, string error, string url = null, string footer = null)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.ErrorColor).WithDescription(error)
                 .WithTitle(title).WithUrl(url).WithFooter(efb => efb.WithText(footer)));

        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string error)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(error));

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string title, string text, string url = null, string footer = null)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(text)
                 .WithTitle(title).WithUrl(url).WithFooter(efb => efb.WithText(footer)));

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string text)
             => ch.SendMessageAsync("", embed: new EmbedBuilder().WithColor(NadekoBot.OkColor).WithDescription(text));

        public static Task<IUserMessage> SendTableAsync<T>(this IMessageChannel ch, string seed, IEnumerable<T> items, Func<T, string> howToPrint, int columns = 3)
        {
            var i = 0;
            return ch.SendMessageAsync($@"{seed}```css
{string.Join("\n", items.GroupBy(item => (i++) / columns)
                        .Select(ig => string.Concat(ig.Select(el => howToPrint(el)))))}
```");
        }

        public static Task<IUserMessage> SendTableAsync<T>(this IMessageChannel ch, IEnumerable<T> items, Func<T, string> howToPrint, int columns = 3) =>
            ch.SendTableAsync("", items, howToPrint, columns);

        /// <summary>
        /// returns an IEnumerable with randomized element order
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> items)
        {
            // Thanks to @Joe4Evr for finding a bug in the old version of the shuffle
            using (var provider = RandomNumberGenerator.Create())
            {
                var list = items.ToList();
                var n = list.Count;
                while (n > 1)
                {
                    var box = new byte[(n / Byte.MaxValue) + 1];
                    int boxSum;
                    do
                    {
                        provider.GetBytes(box);
                        boxSum = box.Sum(b => b);
                    }
                    while (!(boxSum < n * ((Byte.MaxValue * box.Length) / n)));
                    var k = (boxSum % n);
                    n--;
                    var value = list[k];
                    list[k] = list[n];
                    list[n] = value;
                }
                return list;
            }
        }

        public static string TrimTo(this string str, int maxLength, bool hideDots = false)
        {
            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), $"Argument {nameof(maxLength)} can't be negative.");
            if (maxLength == 0)
                return string.Empty;
            if (maxLength <= 3)
                return string.Concat(str.Select(c => '.'));
            if (str.Length < maxLength)
                return str;
            return string.Concat(str.Take(maxLength - 3)) + (hideDots ? "" : "...");
        }

        public static string ToTitleCase(this string str)
        {
            var tokens = str.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                tokens[i] = token.Substring(0, 1).ToUpper() + token.Substring(1);
            }

            return string.Join(" ", tokens);
        }

        /// <summary>
        /// Removes trailing S or ES (if specified) on the given string if the num is 1
        /// </summary>
        /// <param name="str"></param>
        /// <param name="num"></param>
        /// <param name="es"></param>
        /// <returns>String with the correct singular/plural form</returns>
        public static string SnPl(this string str, int? num, bool es = false)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            if (num == null)
                throw new ArgumentNullException(nameof(num));
            return num == 1 ? str.Remove(str.Length - 1, es ? 2 : 1) : str;
        }

        //http://www.dotnetperls.com/levenshtein
        public static int LevenshteinDistance(this string s, string t)
        {
            var n = s.Length;
            var m = t.Length;
            var d = new int[n + 1, m + 1];

            // Step 1
            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            // Step 2
            for (var i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (var j = 0; j <= m; d[0, j] = j++)
            {
            }

            // Step 3
            for (var i = 1; i <= n; i++)
            {
                //Step 4
                for (var j = 1; j <= m; j++)
                {
                    // Step 5
                    var cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            // Step 7
            return d[n, m];
        }

        public static async Task<Stream> ToStream(this string str)
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);
            await sw.WriteAsync(str);
            await sw.FlushAsync();
            ms.Position = 0;
            return ms;

        }

        public static string ToJson<T>(this T any, Formatting formatting = Formatting.Indented) =>
            JsonConvert.SerializeObject(any, formatting);

        public static int KiB(this int value) => value * 1024;
        public static int KB(this int value) => value * 1000;

        public static int MiB(this int value) => value.KiB() * 1024;
        public static int MB(this int value) => value.KB() * 1000;

        public static int GiB(this int value) => value.MiB() * 1024;
        public static int GB(this int value) => value.MB() * 1000;

        public static ulong KiB(this ulong value) => value * 1024;
        public static ulong KB(this ulong value) => value * 1000;

        public static ulong MiB(this ulong value) => value.KiB() * 1024;
        public static ulong MB(this ulong value) => value.KB() * 1000;

        public static ulong GiB(this ulong value) => value.MiB() * 1024;
        public static ulong GB(this ulong value) => value.MB() * 1000;

        public static string Unmention(this string str) => str.Replace("@", "ම");

        public static ImageSharp.Image Merge(this IEnumerable<ImageSharp.Image> images)
        {
            var imgList = images.ToList();

            var canvas = new ImageSharp.Image(imgList.Sum(img => img.Width), imgList.Max(img => img.Height));

            var canvasPixels = canvas.Lock();
            int offsetX = 0;
            foreach (var img in imgList.Select(img => img.Lock()))
            {
                for (int i = 0; i < img.Width; i++)
                {
                    for (int j = 0; j < img.Height; j++)
                    {
                        canvasPixels[i + offsetX, j] = img[i, j];
                    }
                }
                offsetX += img.Width;
            }

            return canvas;
        }

        public static Stream ToStream(this ImageSharp.Image img)
        {
            var imageStream = new MemoryStream();
            img.SaveAsPng(imageStream);
            imageStream.Position = 0;
            return imageStream;
        }

        private static readonly Regex filterRegex = new Regex(@"(?:discord(?:\.gg|.me|app\.com\/invite)\/(?<id>([\w]{16}|(?:[\w]+-?){3})))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsDiscordInvite(this string str)
            => filterRegex.IsMatch(str);
    }
}