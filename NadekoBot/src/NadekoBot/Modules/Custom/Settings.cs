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

namespace NadekoBot.Modules.Settings
{
    [NadekoModule("Settings", ".")]

    public partial class Settings : DiscordModule
    {
        public static Color SettingColor = new Color(1f, 1f, 0.47f);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task CurrencyGenerationChance(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "CurrencyGenerationChance";
            var sb = new StringBuilder();
            
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
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}%` to `{val}%`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }
            
            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task CurrencyDropAmount(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "CurrencyDropAmount";
            var sb = new StringBuilder();

            float CurrentVal = NadekoBot.BotConfig.CurrencyDropAmount;

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
                            NadekoBot.BotConfig.CurrencyDropAmount = val;
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task HangmanCurrencyRewardAll(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "HangmanCurrencyRewardAll";
            var sb = new StringBuilder();

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
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task HangmanCurrencyRewardLetter(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "HangmanCurrencyRewardLetter";
            var sb = new StringBuilder();
            
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
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task TypeStartCurrencyReward(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "TypeStartCurrencyReward";
            var sb = new StringBuilder();
            
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
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task TriviaCurrencyReward(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "TriviaCurrencyReward";
            var sb = new StringBuilder();
            
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
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        [OwnerOnly]
        public async Task TriviaCurrencyRewardMultiplier(string str = null, [Remainder] ITextChannel channel = null)
        {
            channel = (ITextChannel)Context.Channel;
            var cmdname = "TriviaCurrencyRewardMultiplier";
            var sb = new StringBuilder();
            
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
                            sb.AppendLine($":white_check_mark: **{cmdname}** has been changed from `{CurrentVal}` to `{val}`");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($":anger: **{cmdname}** cannot accept text. Please input numbers instead.");
                }
            }

            await channel.EmbedAsync(new EmbedBuilder().WithColor(SettingColor).WithDescription(sb.ToString())).ConfigureAwait(false);
        }
    }
}
