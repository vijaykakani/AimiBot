using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NadekoBot.Services;
using NadekoBot.Services.Database.Models;
using System.Collections.Generic;
using AngleSharp;
using AngleSharp.Parser.Html;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using NadekoBot.Modules.Searches.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System.Net;
using System.IO;

namespace NadekoBot.Modules.Settings
{
    [NadekoModule("RRL", "=")]

    public partial class RoyalRoadL : DiscordModule
    {
        public string royalroadl_domain = "http://royalroadl.com/";

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Story([Remainder] int stody_id = 0)
        {
            string fullQueryLink = royalroadl_domain + "fiction/" + stody_id;
            var embed = new EmbedBuilder();
            string error_message = "";

            AngleSharp.Dom.IDocument document = null;
            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);
            }
            catch (Exception ex)
            {
                error_message += $"Message: {ex.Message}\nSource: searching the site\n";
            }
            
            var existElem = document.QuerySelector("div.col-md-12.page-404");
            if (existElem != null)
            {
                embed.WithErrorColor().WithDescription($"The story with the ID \"{stody_id}\" does not exist.");
            }
            else
            {
                //  title
                var titleElem = document.QuerySelector("h2.font-white");
                string titleText = "";
                try
                {
                    titleText = titleElem.Text();
                }
                catch (Exception ex)
                {
                    error_message += $"Message: {ex.Message}\nSource: title\n";
                }
                
                //  author
                var authorElem = document.QuerySelector("h4.font-white");
                string authorText = "";
                try
                {
                    authorText = authorElem.Text();
                }
                catch (Exception ex)
                {
                    error_message += $"Message: {ex.Message}\nSource: author\n";
                }

                //  description/synopsis
                var descElem = document.QuerySelector("div.hidden-content");
                string descText = "";
                try
                {
                    descText = descElem.InnerHtml.Replace("<hr>", "----------------").Replace("<br>", "\n");
                    descText = Regex.Replace(descText, "<.*?>", string.Empty);
                    //await Context.Channel.EmbedAsync(new EmbedBuilder().WithDescription(descText)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error_message += $"Message: {ex.Message}\nSource: description\n";
                }
                
                //  cover art
                var imageElem = document.QuerySelector("img.img-offset");
                string imageUrl = "";
                try
                {
                    imageUrl = ((IHtmlImageElement)imageElem).Source;
                }
                catch (Exception ex)
                {
                    error_message += $"Message: {ex.Message}\nSource: image\n";
                }

                //  tags
                var tagsElem = document.QuerySelector("span.tags");
                List<string> tagsList = null;
                try
                {
                    string tagsText = Regex.Replace(tagsElem.InnerHtml, "<.*?>", string.Empty);
                    tagsList = tagsText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                catch (Exception ex)
                {
                    error_message += $"Message: {ex.Message}\nSource: tags\n";
                }

                /*
                StringBuilder sb = new StringBuilder();
                for (var i = 0; i < tagsList.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tagsList[i]))
                        sb.AppendLine(tagsList[i] + " " + tagsList.Count);
                }
                await Context.Channel.SendConfirmAsync($"{sb}").ConfigureAwait(false);
                */
                
                embed.WithOkColor().WithTitle(titleText);

                string title = $"**{titleText}** {authorText}\n\n";

                string description = "**Description**\n";
                string desc = descText.Trim();
                if (desc.Length > 0)
                    description += desc + "\n\n";
                else
                    description += "*No description given*\n\n";

                string tags = "**Tags**\n";
                string tags_line = "";
                foreach (var item in tagsList)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                        tags_line += item + ", ";
                }

                if (tags_line.Length > 0)
                {
                    tags_line = tags_line.Substring(0, tags_line.Length - 2);
                    tags += tags_line + "\n\n";
                }
                else
                {
                    tags += "*No tags declared*\n\n";
                }

                embed.WithDescription(title + description + tags);
                embed.WithUrl(fullQueryLink);
                embed.WithImageUrl(imageUrl);
            }

            try
            {
                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error_message += $"Message: {ex.Message}\nSource: sending story message\n";
            }

            if (!string.IsNullOrWhiteSpace(error_message.Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message)).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Search([Remainder] string name = null)
        {
            var channel = (ITextChannel)Context.Channel;

            Color msg_color = new Color();
            string debug_msg = "";
            string display_msg = "";
            string error_message = "";
            int page_no = 1;
            int separation = 0;

            if (!string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    separation = name.IndexOf(" ");
                    if (separation == -1)
                    {
                        if (!int.TryParse(name, out page_no))
                            page_no = 1;
                    }
                    else
                    {
                        string page_text = name.Substring(0, separation);
                        if (int.TryParse(page_text, out page_no))
                        {
                            int new_start = separation + 1;
                            if (new_start < name.Length)
                                name = name.Substring(new_start);
                        }
                        else page_no = 1;
                    }
                }
                catch (Exception ex)
                {
                    error_message += $"Message: {ex.Message}\nSource: Choosing Page Numbers\n";
                }
            }

            string queryPath = $"page={page_no}";
            if (!string.IsNullOrWhiteSpace(name) && (separation != -1))
                queryPath += $"&keyword={Uri.EscapeDataString(name)}";

            string fullQueryLink = royalroadl_domain + $"fictions/search?{queryPath}";
            //await channel.SendConfirmAsync($"{fullQueryLink}").ConfigureAwait(false);

            //debug_msg += $"Link: {fullQueryLink}\n";

            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);

                var rrl_logo_rlm = document.QuerySelector("img.logo-default");
                var rrl_logoUrl = ((IHtmlImageElement)rrl_logo_rlm).Source;

                var stories_elem = document.QuerySelectorAll("li.search-item.clearfix > div.row");
                if (stories_elem.Count() == 0)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        display_msg = $"{stories_elem.Count()} searches found for \"{name}\"";
                    else
                        display_msg = $"{stories_elem.Count()} searches found";
                    msg_color = NadekoBot.ErrorColor;
                }
                else
                {
                    List<string> stories_list = new List<string>();

                    //debug_msg += $"Stories Count: {stories_elem.Count()}\n";
                    //debug_msg += $"Page Number: {page_no}\n";

                    foreach (var item_a in stories_elem)
                    {
                        var parser = new HtmlParser();
                        var story_doc = parser.Parse(item_a.InnerHtml);

                        var story_link_elm = story_doc.QuerySelector("h2.margin-bottom-10 > a");
                        IHtmlAnchorElement story_elm = (IHtmlAnchorElement)story_link_elm;

                        string story_url = story_elm.Href.ToString().Substring(9);
                        string story_link = royalroadl_domain + story_url;
                        string story_title = story_elm.Text();
                        int story_id = int.Parse(story_url.Substring(story_url.IndexOf("/") + 1));
                        //debug_msg += $"[{story_title}]({story_link}) ({story_id})\n

                        var author_text_elm = story_doc.QuerySelector("span.pull-right.author.small");
                        IHtmlSpanElement author_elm = (IHtmlSpanElement)author_text_elm;
                        string author_text = Regex.Replace(author_elm.InnerHtml, "<.*?>", string.Empty).Trim();
                        //debug_msg += $"{author_text}\n";

                        var pages_text_elm = story_doc.QuerySelector("span.page-count.small.uppercase.bold.font-blue-dark");
                        IHtmlSpanElement pages_elm = (IHtmlSpanElement)pages_text_elm;
                        string pages_text = Regex.Replace(pages_elm.InnerHtml, "<.*?>", string.Empty).Trim();

                        stories_list.Add($"[{story_title}]({story_link}) ({story_id}): {pages_text} {author_text}");

                        /*
                        foreach (IHtmlAnchorElement menuLink in story_link_elm)
                        {
                            string story_url = menuLink.Href.ToString().Substring(9);
                            string link  = royalroadl_domain + story_url;
                            string title = menuLink.Text();
                            int story_id = int.Parse(story_url.Substring(story_url.IndexOf("/") + 1));
                            debug_msg += $"[{title}]({link}) ({story_id})\n";
                        }
                        */
                    }

                    msg_color = NadekoBot.OkColor;
                    display_msg = String.Join("\n", stories_list.ToArray()) + "\n\n";

                    var pagination_elem = document.QuerySelectorAll("ul.pagination > li");
                    if (pagination_elem.Count() == 0)
                    {
                        display_msg += $"**Current Page:** `{page_no}`\n";
                        display_msg += $"**Total Pages:** `{page_no}`\n";
                    }
                    else
                    {
                        display_msg += $"**Current Page:** `{page_no}`\n";

                        //debug_msg += $"Pagination: {pagination_elem.Count()}\n";

                        try
                        {
                            foreach (var item_b in pagination_elem)
                            {
                                var parser = new HtmlParser();
                                var page_elem = parser.Parse(item_b.InnerHtml);

                                var page_link_elm = page_elem.QuerySelector("a");
                                IHtmlAnchorElement page_link = (IHtmlAnchorElement)page_link_elm;

                                string page_link_str = page_link.Href.ToString();
                                string page_link_txt = page_link.Text();

                                //debug_msg += page_link_txt + "\n";

                                if (page_link_txt == "Last ›")
                                {
                                    string total_pages = page_link_str.Substring(page_link_str.IndexOf("=") + 1);

                                    display_msg += $"**Total Pages:** `{total_pages}`\n";
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            error_message += $"Message: {ex.Message}\nSource: Pagination\n";
                        }
                    }
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(msg_color).WithTitle("Royal Road L: Stories List").WithDescription(display_msg).WithImageUrl(rrl_logoUrl)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error_message += $"Message: {ex.Message}\nSource: Processing the execution\n";
            }

            if (!string.IsNullOrWhiteSpace(debug_msg))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Debug Report").WithDescription($"{debug_msg}\n")).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error_message.Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message)).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Author([Remainder] string name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var channel = (ITextChannel)Context.Channel;

            string fullQueryLink = royalroadl_domain + "fictions/search?author=" + Uri.EscapeDataString(name);
            string author = "";
            //await channel.SendConfirmAsync($"{fullQueryLink}").ConfigureAwait(false);

            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);

                var rrl_logo_rlm = document.QuerySelector("img.logo-default");
                var rrl_logoUrl = ((IHtmlImageElement)rrl_logo_rlm).Source;

                var stories_elem = document.QuerySelectorAll("li.search-item.clearfix > div.row");
                string debug_msg = "";

                int searches_found = stories_elem.Count();
                if (searches_found == 0)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithTitle($"Royal Road L: Author's Stories")
                        .WithDescription($"{searches_found} searches found for author with the name \"{name}\""))
                        .ConfigureAwait(false);
                }
                else
                {
                    List<string> stories_list = new List<string>();
                    foreach (var item_a in stories_elem)
                    {
                        var parser = new HtmlParser();
                        var story_doc = parser.Parse(item_a.InnerHtml);

                        var story_link_elm = story_doc.QuerySelector("h2.margin-bottom-10 > a");
                        IHtmlAnchorElement story_elm = (IHtmlAnchorElement)story_link_elm;

                        string story_url = story_elm.Href.ToString().Substring(9);
                        string story_link = royalroadl_domain + story_url;
                        string story_title = story_elm.Text();
                        int story_id = int.Parse(story_url.Substring(story_url.IndexOf("/") + 1));
                        //debug_msg += $"[{story_title}]({story_link}) ({story_id})\n";

                        var author_text_elm = story_doc.QuerySelector("span.pull-right.author.small");
                        IHtmlSpanElement author_elm = (IHtmlSpanElement)author_text_elm;
                        string author_text = Regex.Replace(author_elm.InnerHtml, "<.*?>", string.Empty).Trim();
                        //debug_msg += $"{author_text}\n";

                        var pages_text_elm = story_doc.QuerySelector("span.page-count.small.uppercase.bold.font-blue-dark");
                        IHtmlSpanElement pages_elm = (IHtmlSpanElement)pages_text_elm;
                        string pages_text = Regex.Replace(pages_elm.InnerHtml, "<.*?>", string.Empty).Trim();

                        stories_list.Add($"[{story_title}]({story_link}) ({story_id}): {pages_text}");
                        author = author_text.Substring(3);

                        /*
                        foreach (IHtmlAnchorElement menuLink in story_link_elm)
                        {
                            string story_url = menuLink.Href.ToString().Substring(9);
                            string link  = royalroadl_domain + story_url;
                            string title = menuLink.Text();
                            int story_id = int.Parse(story_url.Substring(story_url.IndexOf("/") + 1));
                            debug_msg += $"[{title}]({link}) ({story_id})\n";
                        }
                        */
                    }

                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle($"Royal Road L: {author}'s Stories").WithDescription(String.Join("\n", stories_list.ToArray())).WithImageUrl(rrl_logoUrl)).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(debug_msg))
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Debug Report").WithDescription($"**Message**\n{debug_msg}\n")).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                string message = $"Message: {ex.Message}\nSource: {ex.HelpLink}";
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(message)).ConfigureAwait(false);
            }
        }
    }
}
