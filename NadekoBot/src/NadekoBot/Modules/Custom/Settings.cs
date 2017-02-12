using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.Services;
using NadekoBot.Services.Database.Models;
using System.Collections.Generic;

namespace NadekoBot.Modules.Custom
{
    [NadekoModule("Settings", ".")]

    public partial class Settings : DiscordModule
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task CurrencyGenerationChance([Remainder] string str = null) =>
            CurrencyGenerationChance(str);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task CurrencyGenerationChance(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "CurrencyGenerationChance";
            var sb = new StringBuilder();
            Color clr = new Color(1f, 1f, 0.47f);

            float CurrentVal = NadekoBot.BotConfig.CurrencyGenerationChance * 100;

            if (string.IsNullOrWhiteSpace(str))
                sb.AppendLine($":information_source: **{cmdname}** is currently set to `{CurrentVal}%`");
            else
            {
                float val = 0;
                if (float.TryParse(str, out val))
                {
                    if (val < 0)
                        sb.AppendLine($":anger: **{cmdname}** cannot accept values below `0%`");
                    else
                    {
                        if (val == CurrentVal)
                            sb.AppendLine($":information_source: **{cmdname}** is already set to `{CurrentVal}%`");
                        else
                        {
                            NadekoBot.BotConfig.CurrencyGenerationChance = val / 100;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has bee changed from `{CurrentVal}%` to `{val}%`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }
            
            await channel.EmbedAsync(new EmbedBuilder().WithColor(clr).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task HangmanCurrencyRewardAll([Remainder] string str = null) =>
            HangmanCurrencyRewardAll(str);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task HangmanCurrencyRewardAll(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "HangmanCurrencyRewardAll";
            var sb = new StringBuilder();
            Color clr = new Color(1f, 1f, 0.47f);

            int CurrentVal = NadekoBot.BotConfig.HangmanCurrencyRewardAll;

            if (string.IsNullOrWhiteSpace(str))
                sb.AppendLine($":information_source: **{cmdname}** is currently set to `{CurrentVal}`");
            else
            {
                int val = 0;
                if (int.TryParse(str, out val))
                {
                    if (val < 0)
                        sb.AppendLine($":anger: **{cmdname}** cannot accept values below `0`");
                    else
                    {
                        if (val == CurrentVal)
                            sb.AppendLine($":information_source: **{cmdname}** is already set to `{CurrentVal}`");
                        else
                        {
                            NadekoBot.BotConfig.HangmanCurrencyRewardAll = val;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has bee changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(clr).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task HangmanCurrencyRewardLetter([Remainder] string str = null) =>
            HangmanCurrencyRewardLetter(str);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task HangmanCurrencyRewardLetter(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "HangmanCurrencyRewardLetter";
            var sb = new StringBuilder();
            Color clr = new Color(1f, 1f, 0.47f);

            int CurrentVal = NadekoBot.BotConfig.HangmanCurrencyRewardLetter;

            if (string.IsNullOrWhiteSpace(str))
                sb.AppendLine($":information_source: **{cmdname}** is currently set to `{CurrentVal}`");
            else
            {
                int val = 0;
                if (int.TryParse(str, out val))
                {
                    if (val < 0)
                        sb.AppendLine($":anger: **{cmdname}** cannot accept values below `0`");
                    else
                    {
                        if (val == CurrentVal)
                            sb.AppendLine($":information_source: **{cmdname}** is already set to `{CurrentVal}`");
                        else
                        {
                            NadekoBot.BotConfig.HangmanCurrencyRewardLetter = val;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has bee changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(clr).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task TypeStartCurrencyReward([Remainder] string str = null) =>
            TypeStartCurrencyReward(str);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task TypeStartCurrencyReward(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "TypeStartCurrencyReward";
            var sb = new StringBuilder();
            Color clr = new Color(1f, 1f, 0.47f);

            int CurrentVal = NadekoBot.BotConfig.TypeStartCurrencyReward;

            if (string.IsNullOrWhiteSpace(str))
                sb.AppendLine($":information_source: **{cmdname}** is currently set to `{CurrentVal}`");
            else
            {
                int val = 0;
                if (int.TryParse(str, out val))
                {
                    if (val < 0)
                        sb.AppendLine($":anger: **{cmdname}** cannot accept values below `0`");
                    else
                    {
                        if (val == CurrentVal)
                            sb.AppendLine($":information_source: **{cmdname}** is already set to `{CurrentVal}`");
                        else
                        {
                            NadekoBot.BotConfig.TypeStartCurrencyReward = val;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has bee changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(clr).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task TriviaCurrencyReward([Remainder] string str = null) =>
            TriviaCurrencyReward(str);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task TriviaCurrencyReward(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "TriviaCurrencyReward";
            var sb = new StringBuilder();
            Color clr = new Color(1f, 1f, 0.47f);

            int CurrentVal = NadekoBot.BotConfig.TriviaCurrencyReward;

            if (string.IsNullOrWhiteSpace(str))
                sb.AppendLine($":information_source: **{cmdname}** is currently set to `{CurrentVal}`");
            else
            {
                int val = 0;
                if (int.TryParse(str, out val))
                {
                    if (val < 0)
                        sb.AppendLine($":anger: **{cmdname}** cannot accept values below `0`");
                    else
                    {
                        if (val == CurrentVal)
                            sb.AppendLine($":information_source: **{cmdname}** is already set to `{CurrentVal}`");
                        else
                        {
                            NadekoBot.BotConfig.TriviaCurrencyReward = val;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has bee changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(clr).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(1)]
        public Task TriviaCurrencyRewardMultiplier([Remainder] string str = null) =>
            TriviaCurrencyRewardMultiplier(str);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task TriviaCurrencyRewardMultiplier(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "TriviaCurrencyRewardMultiplier";
            var sb = new StringBuilder();
            Color clr = new Color(1f, 1f, 0.47f);

            int CurrentVal = NadekoBot.BotConfig.TriviaCurrencyRewardMultiplier;

            if (string.IsNullOrWhiteSpace(str))
                sb.AppendLine($":information_source: **{cmdname}** is currently set to `{CurrentVal}`");
            else
            {
                int val = 0;
                if (int.TryParse(str, out val))
                {
                    if (val < 0)
                        sb.AppendLine($":anger: **{cmdname}** cannot accept values below `0`");
                    else
                    {
                        if (val == CurrentVal)
                            sb.AppendLine($":information_source: **{cmdname}** is already set to `{CurrentVal}`");
                        else
                        {
                            NadekoBot.BotConfig.TriviaCurrencyRewardMultiplier = val;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has bee changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(clr).WithDescription(sb.ToString())).ConfigureAwait(false);
        }
    }
}
