﻿using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Services;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NadekoBot.Extensions;
using System.Xml;
using System.Threading;
using System.Collections.Concurrent;

namespace NadekoBot.Modules.NSFW
{
    [NadekoModule("NSFW", "~")]
    public class NSFW : DiscordModule
    {
#if !GLOBAL_NADEKO
        private static ConcurrentDictionary<ulong, Timer> AutoHentaiTimers { get; } = new ConcurrentDictionary<ulong, Timer>();
        private static ConcurrentHashSet<ulong> _hentaiBombBlacklist { get; } = new ConcurrentHashSet<ulong>();

        private async Task InternalHentai(IMessageChannel channel, string tag, bool noError)
        {
            tag = tag?.Trim() ?? "";

            tag = "rating%3Aexplicit+" + tag;

            var rng = new NadekoRandom();
            Task<string> provider = Task.FromResult("");
            switch (rng.Next(0, 4))
            {
                case 0:
                    provider = GetDanbooruImageLink(tag);
                    break;
                case 1:
                    provider = GetGelbooruImageLink(tag);
                    break;
                case 2:
                    provider = GetKonachanImageLink(tag);
                    break;
                case 3:
                    provider = GetYandereImageLink(tag);
                    break;
                default:
                    break;
            }
            var link = await provider.ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(link))
            {
                if (!noError)
                    await channel.SendErrorAsync("No results found.").ConfigureAwait(false);
                return;
            }

            await channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithImageUrl(link)
                .WithDescription("Tag: " + tag))
                .ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public Task Hentai([Remainder] string tag = null) =>
            InternalHentai(Context.Channel, tag, false);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task AutoHentai(int interval = 0, string tags = null)
        {
            Timer t;

            if (interval == 0)
            {
                if (AutoHentaiTimers.TryRemove(Context.Channel.Id, out t))
                {
                    t.Change(Timeout.Infinite, Timeout.Infinite); //proper way to disable the timer
                    await Context.Channel.SendConfirmAsync("Autohentai stopped.").ConfigureAwait(false);
                }
                return;
            }

            if (interval < 20)
                return;

            var tagsArr = tags?.Split('|');

            t = new Timer(async (state) =>
            {
                try
                {
                    if (tagsArr == null || tagsArr.Length == 0)
                        await InternalHentai(Context.Channel, null, true).ConfigureAwait(false);
                    else
                        await InternalHentai(Context.Channel, tagsArr[new NadekoRandom().Next(0, tagsArr.Length)], true).ConfigureAwait(false);
                }
                catch { }
            }, null, interval * 1000, interval * 1000);

            AutoHentaiTimers.AddOrUpdate(Context.Channel.Id, t, (key, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return t;
            });

            await Context.Channel.SendConfirmAsync($"Autohentai started. Reposting every {interval}s with one of the following tags:\n{string.Join(", ", tagsArr)}")
                                 .ConfigureAwait(false);
        }


        [NadekoCommand, Usage, Description, Aliases]
        public async Task HentaiBomb([Remainder] string tag = null)
        {
            if (!_hentaiBombBlacklist.Add(Context.User.Id))
                return;
            try
            {
                tag = tag?.Trim() ?? "";
                tag = "rating%3Aexplicit+" + tag;

                var links = await Task.WhenAll(GetGelbooruImageLink(tag),
                                               GetDanbooruImageLink(tag),
                                               GetKonachanImageLink(tag),
                                               GetYandereImageLink(tag)).ConfigureAwait(false);

                var linksEnum = links?.Where(l => l != null);
                if (links == null || !linksEnum.Any())
                {
                    await Context.Channel.SendErrorAsync("No results found.").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendMessageAsync(String.Join("\n\n", linksEnum)).ConfigureAwait(false);
            }
            finally {
                await Task.Delay(5000).ConfigureAwait(false);
                _hentaiBombBlacklist.TryRemove(Context.User.Id);
            }
        }


        [NadekoCommand, Usage, Description, Aliases]
        public async Task Danbooru([Remainder] string tag = null)
        {
            tag = tag?.Trim() ?? "";

            var url = await GetDanbooruImageLink(tag).ConfigureAwait(false);

            if (url == null)
                await Context.Channel.SendErrorAsync(Context.User.Mention + " No results.").ConfigureAwait(false);
            else
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription(Context.User.Mention + " " + tag)
                    .WithImageUrl(url)
                    .WithFooter(efb => efb.WithText("Danbooru")))
                    .ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public Task Yandere([Remainder] string tag = null)
            => Searches.Searches.InternalDapiCommand(Context.Message, tag, Searches.Searches.DapiSearchType.Yandere);

        [NadekoCommand, Usage, Description, Aliases]
        public Task Konachan([Remainder] string tag = null)
            => Searches.Searches.InternalDapiCommand(Context.Message, tag, Searches.Searches.DapiSearchType.Konachan);

        [NadekoCommand, Usage, Description, Aliases]
        public Task Gelbooru([Remainder] string tag = null)
            => Searches.Searches.InternalDapiCommand(Context.Message, tag, Searches.Searches.DapiSearchType.Gelbooru);

        [NadekoCommand, Usage, Description, Aliases]
        public Task Rule34([Remainder] string tag = null)
            => Searches.Searches.InternalDapiCommand(Context.Message, tag, Searches.Searches.DapiSearchType.Rule34);

        [NadekoCommand, Usage, Description, Aliases]
        public async Task E621([Remainder] string tag = null)
        {
            tag = tag?.Trim() ?? "";

            var url = await GetE621ImageLink(tag).ConfigureAwait(false);

            if (url == null)
                await Context.Channel.SendErrorAsync(Context.User.Mention + " No results.");
            else
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription(Context.User.Mention + " " + tag)
                    .WithImageUrl(url)
                    .WithFooter(efb => efb.WithText("e621")))
                    .ConfigureAwait(false);
        }
#endif
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Cp()
        {
            await Context.Channel.SendMessageAsync("http://i.imgur.com/MZkY1md.jpg").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Boobs()
        {
            try
            {
                JToken obj;
                using (var http = new HttpClient())
                {
                    obj = JArray.Parse(await http.GetStringAsync($"http://api.oboobs.ru/boobs/{ new NadekoRandom().Next(0, 10330) }").ConfigureAwait(false))[0];
                }
                await Context.Channel.SendMessageAsync($"http://media.oboobs.ru/{ obj["preview"].ToString() }").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Context.Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Butts()
        {
            try
            {
                JToken obj;
                using (var http = new HttpClient())
                {
                    obj = JArray.Parse(await http.GetStringAsync($"http://api.obutts.ru/butts/{ new NadekoRandom().Next(0, 4335) }").ConfigureAwait(false))[0];
                }
                await Context.Channel.SendMessageAsync($"http://media.obutts.ru/{ obj["preview"].ToString() }").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Context.Channel.SendErrorAsync(ex.Message).ConfigureAwait(false);
            }
        }
#if !GLOBAL_NADEKO
        public static Task<string> GetDanbooruImageLink(string tag) => Task.Run(async () =>
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.AddFakeHeaders();
                    var data = await http.GetStreamAsync("https://danbooru.donmai.us/posts.xml?limit=100&tags=" + tag).ConfigureAwait(false);
                    var doc = new XmlDocument();
                    doc.Load(data);
                    var nodes = doc.GetElementsByTagName("file-url");

                    var node = nodes[new NadekoRandom().Next(0, nodes.Count)];
                    return "https://danbooru.donmai.us" + node.InnerText;
                }
            }
            catch
            {
                return null;
            }
        });


        public static Task<string> GetE621ImageLink(string tag) => Task.Run(async () =>
        {
            try
            {
                using (var http = new HttpClient())
                {
                    http.AddFakeHeaders();
                    var data = await http.GetStreamAsync("http://e621.net/post/index.xml?tags=" + tag).ConfigureAwait(false);
                    var doc = new XmlDocument();
                    doc.Load(data);
                    var nodes = doc.GetElementsByTagName("file_url");

                    var node = nodes[new NadekoRandom().Next(0, nodes.Count)];
                    return node.InnerText;
                }
            }
            catch
            {
                return null;
            }
        });

        public static Task<string> GetYandereImageLink(string tag) =>
            Searches.Searches.InternalDapiSearch(tag, Searches.Searches.DapiSearchType.Yandere);

        public static Task<string> GetKonachanImageLink(string tag) =>
            Searches.Searches.InternalDapiSearch(tag, Searches.Searches.DapiSearchType.Konachan);

        public static Task<string> GetGelbooruImageLink(string tag) =>
            Searches.Searches.InternalDapiSearch(tag, Searches.Searches.DapiSearchType.Gelbooru);

        public static Task<string> GetRule34ImageLink(string tag) =>
            Searches.Searches.InternalDapiSearch(tag, Searches.Searches.DapiSearchType.Rule34);
#endif
    }
}