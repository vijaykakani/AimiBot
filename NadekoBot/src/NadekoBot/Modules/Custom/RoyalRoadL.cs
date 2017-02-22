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
        public async Task Story(int story_id = 0)
        {
            string fullQueryLink = royalroadl_domain + "fiction/" + story_id;
            var embed = new EmbedBuilder();
            StringBuilder error_message = new StringBuilder();

            AngleSharp.Dom.IDocument document = null;
            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: searching the site");
            }

            var existElem = document.QuerySelector("div.col-md-12.page-404");
            if (existElem != null)
            {
                embed.WithErrorColor().WithDescription($"The story with the ID \"{story_id}\" does not exist.");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: title");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: author");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: description");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: image");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: tags");
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
                error_message.AppendLine($"Message: {ex.Message}\nSource: sending story message");
            }

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Search([Remainder] string name = null)
        {
            var channel = (ITextChannel)Context.Channel;

            Color msg_color = new Color();
            StringBuilder debug_msg = new StringBuilder();
            StringBuilder display_msg = new StringBuilder();
            StringBuilder error_message = new StringBuilder();
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
                        else
                            name = "";
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: Choosing Page Numbers\n");
                }
            }

            string queryPath = $"page={page_no}";
            if (!string.IsNullOrWhiteSpace(name))
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
                        display_msg.AppendLine($"{stories_elem.Count()} searches found for \"{name}\"");
                    else
                        display_msg.AppendLine($"{stories_elem.Count()} searches found");
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
                    display_msg.AppendLine(String.Join("\n", stories_list.ToArray()) + "\n");

                    var pagination_elem = document.QuerySelectorAll("ul.pagination > li");
                    if (pagination_elem.Count() > 0)
                    {
                        display_msg.AppendLine($"**Current Page:** `{page_no}`");

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

                                    display_msg.AppendLine($"**Total Pages:** `{total_pages}`\n");
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            error_message.AppendLine($"Message: {ex.Message}\nSource: Pagination");
                        }
                    }
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(msg_color).WithTitle("Royal Road L: Stories List").WithDescription(display_msg.ToString()).WithImageUrl(rrl_logoUrl)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Processing the execution");
            }

            if (!string.IsNullOrWhiteSpace(debug_msg.ToString()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Debug Report").WithDescription($"{debug_msg}\n")).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
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

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Tags([Remainder] string name = null)
        {
            var channel = (ITextChannel)Context.Channel;

            Color msg_color = new Color();
            StringBuilder debug_msg = new StringBuilder();
            StringBuilder display_msg = new StringBuilder();
            StringBuilder error_message = new StringBuilder();
            int page_no = 1;
            int separation = 0;
            List<string> tags_list = new List<string>();

            string fullQueryLink = royalroadl_domain + "fictions/search";

            if (string.IsNullOrWhiteSpace(name))
            {
                //debug_msg.AppendLine("Test");

                var config = Configuration.Default.WithDefaultLoader();
                var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);

                var rrl_logo_rlm = document.QuerySelector("img.logo-default");
                var rrl_logoUrl = ((IHtmlImageElement)rrl_logo_rlm).Source;

                try
                {
                    //var tags_elems = document.QuerySelectorAll("span.tag-label");
                    var tags_elems = document.QuerySelectorAll("button.btn.default.search-tag");
                    int max_len = 0;
                    //debug_msg.AppendLine($"{tags_elems.Count().ToString()}");
                    foreach (var tag in tags_elems)
                    {
                        /*
                        if (!string.IsNullOrWhiteSpace(tag.Text()))
                            tags_list.Add(tag.Text().Trim());*/
                        
                        string tag_code = tag.GetAttribute("data-tag");
                        string tag_name = tag.GetAttribute("data-label");

                        if (tag_name.Length > max_len)
                            max_len = tag_name.Length + 1;

                        /*debug_msg.AppendLine($"tag_code: {tag_code}");
                        debug_msg.AppendLine($"tag_name: {tag_name}");

                        await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Debug Report").WithDescription($"{debug_msg.ToString()}")).ConfigureAwait(false);
                        debug_msg.Clear();
                        break;*/
                    }

                    foreach (var tag in tags_elems)
                    {
                        string tag_code = tag.GetAttribute("data-tag");
                        string tag_name = tag.GetAttribute("data-label");
                        for (int i = tag_name.Length; i < max_len; i++)
                            tag_name += " ";

                        string tag_line = tag_name + "(" + tag_code + ")";
                        tags_list.Add(tag_line);
                    }
                }
                catch (Exception ex)
                {
                    error_message.AppendLine($"Message: {ex.Message}\nSource: Doing Tags List");
                }

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle($"Royal Road L: Tags").WithDescription(String.Join("\n", tags_list.ToArray())).WithImageUrl(rrl_logoUrl)).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    separation = name.IndexOf(" ");
                    if (separation == -1)
                    {
                        if (!int.TryParse(name, out page_no))
                            page_no = 1;
                        else
                            name = "";
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
                        else
                            page_no = 1;
                    }
                }
                catch (Exception ex)
                {
                    error_message.AppendLine($"Message: {ex.Message}\nSource: Choosing Page Numbers");
                }

                //debug_msg.AppendLine($"param: {name}");

                string queryPath = $"page={page_no}";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    tags_list = name.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    foreach (var tag in tags_list)
                    {
                        if (!string.IsNullOrWhiteSpace(tag.Trim()))
                        {
                            string path = "&tagsAdd=" + Uri.EscapeDataString(tag.Trim());
                            queryPath += path;
                            //debug_msg.AppendLine(path);
                        }
                    }
                }

                try
                {
                    fullQueryLink += $"?{queryPath}";
                    //debug_msg.AppendLine(fullQueryLink);

                    var config = Configuration.Default.WithDefaultLoader();
                    var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);

                    var rrl_logo_rlm = document.QuerySelector("img.logo-default");
                    var rrl_logoUrl = ((IHtmlImageElement)rrl_logo_rlm).Source;

                    var stories_elem = document.QuerySelectorAll("li.search-item.clearfix > div.row");
                    if (stories_elem.Count() == 0)
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                            display_msg.AppendLine($"{stories_elem.Count()} searches found for \"{name}\"");
                        else
                            display_msg.AppendLine($"{stories_elem.Count()} searches found");
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
                        display_msg.AppendLine(String.Join("\n", stories_list.ToArray()) + "\n");

                        var pagination_elem = document.QuerySelectorAll("ul.pagination > li");
                        if (pagination_elem.Count() > 0)
                        {
                            display_msg.AppendLine($"**Current Page:** `{page_no}`\n");

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
                                        int start_point = page_link_str.IndexOf("=") + 1;
                                        int end_point = page_link_str.IndexOf("&tagsAdd=");
                                        string total_pages = page_link_str.Substring(start_point, end_point - start_point);

                                        display_msg.AppendLine($"**Total Pages:** `{total_pages}`\n");
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                error_message.AppendLine($"Message: {ex.Message}\nSource: Pagination\n");
                            }
                        }

                        if (tags_list.Count() > 0)
                            display_msg.AppendLine($"**Searched Tags:** `{String.Join(", ", tags_list.ToArray())}`");
                    }

                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithColor(msg_color).WithTitle("Royal Road L: Stories List").WithDescription(display_msg.ToString()).WithImageUrl(rrl_logoUrl)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error_message.AppendLine($"Message: {ex.Message}\nSource: Processing the execution\n");
                }
            }

            if (!string.IsNullOrWhiteSpace(debug_msg.ToString()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Debug Report").WithDescription($"{debug_msg.ToString()}")).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stats(int story_id = 0)
        {
            string fullQueryLink = royalroadl_domain + "fiction/" + story_id;
            var embed = new EmbedBuilder();
            StringBuilder error_message = new StringBuilder();

            AngleSharp.Dom.IDocument document = null;
            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                document = await BrowsingContext.New(config).OpenAsync(fullQueryLink);
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: searching the site");
            }

            var existElem = document.QuerySelector("div.col-md-12.page-404");
            if (existElem != null)
            {
                embed.WithErrorColor().WithDescription($"The story with the ID \"{story_id}\" does not exist.");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: title");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: author");
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
                    error_message.AppendLine($"Message: {ex.Message}\nSource: image");
                }

                var display = new StringBuilder();
                display.AppendLine($"**{titleText}** {authorText}\n");

                //  stats base
                var div_stats_elems = document.QuerySelectorAll("div.stats-content > div.col-sm-6");

                //  score
                var score_parser = new HtmlParser();
                var score_p_elem = score_parser.Parse(div_stats_elems[0].InnerHtml);
                var score_elems = score_p_elem.QuerySelectorAll("span.star");
                display.AppendLine("**Scores**");
                try
                {
                    foreach (var score_elm in score_elems)
                    {
                        var s_title = score_elm.GetAttribute("data-original-title");
                        var s_stars = score_elm.GetAttribute("data-content");
                        
                        display.AppendLine($"`{s_title}:` {s_stars} 🌟");
                    }
                }
                catch (Exception ex)
                {
                    error_message.AppendLine($"Message: {ex.Message}\nSource: scores");
                }

                display.AppendLine("");
                
                //  count
                var count_parser = new HtmlParser();
                var count_p_elem = score_parser.Parse(div_stats_elems[1].InnerHtml);
                var count_elems = count_p_elem.QuerySelectorAll("li.bold.uppercase");
                display.AppendLine("**Count**");
                try
                {
                    for (var i = 0; i < count_elems.Count();)
                    {
                        var str = $"`{count_elems[i++].Text()}`";
                        str += $" {count_elems[i++].Text()}";
                        display.AppendLine(str);
                    }
                }
                catch (Exception ex)
                {
                    error_message.AppendLine($"Message: {ex.Message}\nSource: stats");
                }

                embed.WithOkColor().WithTitle(titleText);
                embed.WithDescription(display.ToString());
                embed.WithUrl(fullQueryLink);
                embed.WithImageUrl(imageUrl);
            }

            try
            {
                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: sending story message");
            }

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
        }
    }
}
