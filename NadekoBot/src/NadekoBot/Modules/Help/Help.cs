﻿using Discord.Commands;
using NadekoBot.Extensions;
using System.Linq;
using Discord;
using NadekoBot.Services;
using System.Threading.Tasks;
using NadekoBot.Attributes;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace NadekoBot.Modules.Help
{
    [NadekoModule("Help", "-")]
    public partial class Help : DiscordModule
    {
        private static string helpString { get; } = NadekoBot.BotConfig.HelpString;
        public static string HelpString => String.Format(helpString, NadekoBot.Credentials.ClientId, NadekoBot.ModulePrefixes[typeof(Help).Name]);

        public static string DMHelpString { get; } = NadekoBot.BotConfig.DMHelpString;

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Modules()
        {

            var embed = new EmbedBuilder().WithOkColor().WithFooter(efb => efb.WithText($" ℹ️ Type `-cmds ModuleName` to get a list of commands in that module. eg `-cmds games`"))
                .WithTitle("📜 List Of Modules").WithDescription("\n• " + string.Join("\n• ", NadekoBot.CommandService.Modules.GroupBy(m => m.GetTopLevelModule()).Select(m => m.Key.Name).OrderBy(s => s)));
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Commands([Remainder] string module = null)
        {
            var channel = Context.Channel;

            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
                return;
            var cmds = NadekoBot.CommandService.Commands.Where(c => c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(module))
                                                  .OrderBy(c => c.Aliases.First())
                                                  .Distinct(new CommandTextEqualityComparer())
                                                  .AsEnumerable();

            var cmdsArray = cmds as CommandInfo[] ?? cmds.ToArray();
            if (!cmdsArray.Any())
            {
                await channel.SendErrorAsync("That module does not exist.").ConfigureAwait(false);
                return;
            }
            if (module != "customreactions" && module != "conversations")
            {
                await channel.SendTableAsync("📃 **List Of Commands:**\n", cmdsArray, el => $"{el.Aliases.First(),-15} {"["+el.Aliases.Skip(1).FirstOrDefault()+"]",-8}").ConfigureAwait(false);
            }
            else
            {
                await channel.SendMessageAsync("📃 **List Of Commands:**\n• " + string.Join("\n• ", cmdsArray.Select(c => $"{c.Aliases.First()}")));
            }
            await channel.SendConfirmAsync($"ℹ️ **Type** `\"{NadekoBot.ModulePrefixes[typeof(Help).Name]}h CommandName\"` **to see the help for that specified command.** ***e.g.*** `-h >8ball`").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task H([Remainder] string comToFind = null)
        {
            var channel = Context.Channel;

            comToFind = comToFind?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(comToFind))
            {
                IMessageChannel ch = channel is ITextChannel ? await ((IGuildUser)Context.User).CreateDMChannelAsync() : channel;
                await ch.SendMessageAsync(HelpString).ConfigureAwait(false);
                return;
            }
            var com = NadekoBot.CommandService.Commands.FirstOrDefault(c => c.Aliases.Select(a=>a.ToLowerInvariant()).Contains(comToFind));

            if (com == null)
            {
                await channel.SendErrorAsync("I can't find that command. Please check the **command** and **command prefix** before trying again.");
                return;
            }
            var str = $"**`{com.Aliases.First()}`**";
            var alias = com.Aliases.Skip(1).FirstOrDefault();
            if (alias != null)
                str += $" **/ `{alias}`**";
                var embed = new EmbedBuilder()
                .AddField(fb => fb.WithName(str).WithValue($"{ string.Format(com.Summary, com.Module.Aliases.First())} { GetCommandRequirements(com)}").WithIsInline(true))
                .AddField(fb => fb.WithName("**Usage**").WithValue($"{string.Format(com.Remarks, com.Module.Aliases.First())}").WithIsInline(false))
                .WithColor(NadekoBot.OkColor);
            await channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private string GetCommandRequirements(CommandInfo cmd) => 
            String.Join(" ", cmd.Preconditions
                  .Where(ca => ca is OwnerOnlyAttribute || ca is RequireUserPermissionAttribute)
                  .Select(ca =>
                  {
                      if (ca is OwnerOnlyAttribute)
                          return "**Bot Owner only.**";
                      var cau = (RequireUserPermissionAttribute)ca;
                      if (cau.GuildPermission != null)
                          return $"**Requires {cau.GuildPermission} server permission.**".Replace("Guild", "Server");
                      else
                          return $"**Requires {cau.ChannelPermission} channel permission.**".Replace("Guild", "Server");
                  }));

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Hgit()
        {
            var helpstr = new StringBuilder();
            helpstr.AppendLine("You can support the project on patreon: <https://patreon.com/nadekobot> or paypal: <https://www.paypal.me/Kwoth>\n");
            helpstr.AppendLine("##Table Of Contents");
            helpstr.AppendLine(string.Join("\n", NadekoBot.CommandService.Modules.Where(m => m.GetTopLevelModule().Name.ToLowerInvariant() != "help")
                .Select(m => m.GetTopLevelModule().Name)
                .Distinct()
                .OrderBy(m => m)
                .Prepend("Help")
                .Select(m => $"- [{m}](#{m.ToLowerInvariant()})")));
            helpstr.AppendLine();
            string lastModule = null;
            foreach (var com in NadekoBot.CommandService.Commands.OrderBy(com => com.Module.GetTopLevelModule().Name).GroupBy(c => c.Aliases.First()).Select(g => g.First()))
            {
                var module = com.Module.GetTopLevelModule();
                if (module.Name != lastModule)
                {
                    if (lastModule != null)
                    {
                        helpstr.AppendLine();
                        helpstr.AppendLine("###### [Back to TOC](#table-of-contents)");
                    }
                    helpstr.AppendLine();
                    helpstr.AppendLine("### " + module.Name + "  ");
                    helpstr.AppendLine("Command and aliases | Description | Usage");
                    helpstr.AppendLine("----------------|--------------|-------");
                    lastModule = module.Name;
                }
                helpstr.AppendLine($"{string.Join(" ", com.Aliases.Select(a => "`" + a + "`"))} | {string.Format(com.Summary, com.Module.GetPrefix())} {GetCommandRequirements(com)} | {string.Format(com.Remarks, com.Module.GetPrefix())}");
            }
            helpstr = helpstr.Replace(NadekoBot.Client.CurrentUser.Username , "@BotName");
            File.WriteAllText("../../docs/Commands List.md", helpstr.ToString());
            await Context.Channel.SendConfirmAsync("Commandlist Regenerated").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Guide()
        {
            var channel = (ITextChannel)Context.Channel;

            await channel.SendConfirmAsync(
@"**LIST OF COMMANDS**: <http://nadekobot.readthedocs.io/en/latest/Commands%20List/>
**Hosting Guides and docs can be found here**: <http://nadekobot.readthedocs.io/en/latest/>").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Donate()
        {
            var channel = (ITextChannel)Context.Channel;

            await channel.SendConfirmAsync(
$@"You can support the NadekoBot project on patreon. <https://patreon.com/nadekobot> or
Paypal <https://paypal.me/Kwoth>
Don't forget to leave your discord name or id in the message.

**Thank you** ♥️").ConfigureAwait(false);
        }
    }

    public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
    {
        public bool Equals(CommandInfo x, CommandInfo y) => x.Aliases.First() == y.Aliases.First();

        public int GetHashCode(CommandInfo obj) => obj.Aliases.First().GetHashCode();

    }
}
