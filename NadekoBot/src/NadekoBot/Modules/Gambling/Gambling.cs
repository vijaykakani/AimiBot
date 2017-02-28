﻿using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.Services;
using NadekoBot.Services.Database.Models;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System;
using System.IO;

namespace NadekoBot.Modules.Gambling
{
    [NadekoModule("Gambling", "$")]
    public partial class Gambling : DiscordModule
    {
        public static string CurrencyName { get; set; }
        public static string CurrencyPluralName { get; set; }
        public static string CurrencySign { get; set; }

        static Gambling()
        {
            CurrencyName = NadekoBot.BotConfig.CurrencyName;
            CurrencyPluralName = NadekoBot.BotConfig.CurrencyPluralName;
            CurrencySign = NadekoBot.BotConfig.CurrencySign;
        }

        public static long GetCurrency(ulong id)
        {
            using (var uow = DbHandler.UnitOfWork())
            {
                return uow.Currency.GetUserCurrency(id);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Raffle([Remainder] IRole role = null)
        {
            role = role ?? Context.Guild.EveryoneRole;

            var members = role.Members().Where(u => u.Status != UserStatus.Offline && u.Status != UserStatus.Unknown);
            var membersArray = members as IUser[] ?? members.ToArray();
            var usr = membersArray[new NadekoRandom().Next(0, membersArray.Length)];
            await Context.Channel.SendConfirmAsync("🎟 Raffled user", $"**{usr.Username}#{usr.Discriminator}**", footer: $"ID: {usr.Id}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(0)]
        public async Task Cash([Remainder] IUser user = null)
        {
            user = user ?? Context.User;
            var guildUser = user as IGuildUser;

            var root_folder = "C:\\Users\\Ooha\\GitHub\\AimiBot\\NadekoBot\\src\\NadekoBot\\bin\\Release\\netcoreapp1.0\\data\\images/rank/";

            var bakImgPath = $"{root_folder}bg.png";

            System.Drawing.Image backImg = new Bitmap(bakImgPath);
            System.Drawing.Image mrkImg = null;
            try
            {
                HttpClient client = new HttpClient(); // Create HttpClient
                byte[] buffer = await client.GetByteArrayAsync(user.AvatarUrl); // Download file
                mrkImg = System.Drawing.Image.FromStream(new MemoryStream(buffer));
            }
            catch (Exception ex)
            {
                await Context.Channel.SendErrorAsync(ex.Message);
            }
            
            Graphics g = Graphics.FromImage(backImg);
            g.DrawImage(mrkImg, 20, 60);
            g.DrawString(user.Username.ToUpperInvariant(), new Font("Bitter", 20, FontStyle.Regular), Brushes.White, 30, 15);
            g.DrawString(GetCurrency(user.Id).ToString(), new Font("Exo", 16, FontStyle.Regular), Brushes.Black, 215, 120);

            var g_id = Guid.NewGuid().ToString("N");
            var r_path = $"{root_folder}result_{g_id}.jpg";
            backImg.Save(r_path);
            backImg.Dispose();

            //  show the file
            await Context.Channel.SendFileAsync(
                                File.Open(r_path, FileMode.OpenOrCreate),
                                new FileInfo(r_path).Name, $":information_source: {guildUser.Nickname}'s Money!")
                                    .ConfigureAwait(false);

            //  remove it once the upload is complete
            File.Delete(r_path);

            //await Context.Channel.SendConfirmAsync($"{user.Username} has {GetCurrency(user.Id)} {CurrencySign}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(1)]
        public async Task Cash(ulong userId)
        {
            await Context.Channel.SendConfirmAsync($"`{userId}` has {GetCurrency(userId)} {CurrencySign}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Give(long amount, [Remainder] IGuildUser receiver)
        {
            if (amount <= 0 || Context.User.Id == receiver.Id)
                return;
            var success = await CurrencyHandler.RemoveCurrencyAsync((IGuildUser)Context.User, $"Gift to {receiver.Username} ({receiver.Id}).", amount, false).ConfigureAwait(false);
            if (!success)
            {
                await Context.Channel.SendErrorAsync($"{Context.User.Mention} You don't have enough {CurrencyPluralName}.").ConfigureAwait(false);
                return;
            }
            await CurrencyHandler.AddCurrencyAsync(receiver, $"Gift from {Context.User.Username} ({Context.User.Id}).", amount, true).ConfigureAwait(false);
            await Context.Channel.SendConfirmAsync($"{Context.User.Mention} gifted {amount}{CurrencySign} to {Format.Bold(receiver.ToString())}!").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(2)]
        public Task Award(int amount, [Remainder] IGuildUser usr) =>
            Award(amount, usr.Id);

        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        [Priority(1)]
        public async Task Award(int amount, ulong usrId)
        {
            if (amount <= 0)
                return;

            await CurrencyHandler.AddCurrencyAsync(usrId, $"Awarded by bot owner. ({Context.User.Username}/{Context.User.Id})", amount).ConfigureAwait(false);

            await Context.Channel.SendConfirmAsync($"{Context.User.Mention} awarded {amount}{CurrencySign} to <@{usrId}>!").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        [Priority(0)]
        public async Task Award(int amount, [Remainder] IRole role)
        {
            var channel = (ITextChannel)Context.Channel;
            var users = (await Context.Guild.GetUsersAsync())
                               .Where(u => u.GetRoles().Contains(role))
                               .ToList();
            await Task.WhenAll(users.Select(u => CurrencyHandler.AddCurrencyAsync(u.Id,
                                                      $"Awarded by bot owner to **{role.Name}** role. ({Context.User.Username}/{Context.User.Id})",
                                                      amount)))
                         .ConfigureAwait(false);

            await Context.Channel.SendConfirmAsync($"Awarded `{amount}` {CurrencyPluralName} to `{users.Count}` users from `{role.Name}` role.")
                         .ConfigureAwait(false);

        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Take(long amount, [Remainder] IGuildUser user)
        {
            if (amount <= 0)
                return;

            if (await CurrencyHandler.RemoveCurrencyAsync(user, $"Taken by bot owner.({Context.User.Username}/{Context.User.Id})", amount, true).ConfigureAwait(false))
                await Context.Channel.SendConfirmAsync($"{Context.User.Mention} successfully took {amount} {(amount == 1 ? CurrencyName : CurrencyPluralName)} from {user}!").ConfigureAwait(false);
            else
                await Context.Channel.SendErrorAsync($"{Context.User.Mention} was unable to take {amount} {(amount == 1 ? CurrencyName : CurrencyPluralName)} from {user} because the user doesn't have that much {CurrencyPluralName}!").ConfigureAwait(false);
        }


        [NadekoCommand, Usage, Description, Aliases]
        [OwnerOnly]
        public async Task Take(long amount, [Remainder] ulong usrId)
        {
            if (amount <= 0)
                return;

            if (await CurrencyHandler.RemoveCurrencyAsync(usrId, $"Taken by bot owner.({Context.User.Username}/{Context.User.Id})", amount).ConfigureAwait(false))
                await Context.Channel.SendConfirmAsync($"{Context.User.Mention} successfully took {amount} {(amount == 1 ? CurrencyName : CurrencyPluralName)} from <@{usrId}>!").ConfigureAwait(false);
            else
                await Context.Channel.SendErrorAsync($"{Context.User.Mention} was unable to take {amount} {(amount == 1 ? CurrencyName : CurrencyPluralName)} from `{usrId}` because the user doesn't have that much {CurrencyPluralName}!").ConfigureAwait(false);
        }

        //[NadekoCommand, Usage, Description, Aliases]
        //[OwnerOnly]
        //public Task BrTest(int tests = 1000)
        //{
        //    var t = Task.Run(async () =>
        //    {
        //        if (tests <= 0)
        //            return;
        //        //multi vs how many times it occured
        //        var dict = new Dictionary<int, int>();
        //        var generator = new NadekoRandom();
        //        for (int i = 0; i < tests; i++)
        //        {
        //            var rng = generator.Next(0, 101);
        //            var mult = 0;
        //            if (rng < 67)
        //            {
        //                mult = 0;
        //            }
        //            else if (rng < 91)
        //            {
        //                mult = 2;
        //            }
        //            else if (rng < 100)
        //            {
        //                mult = 4;
        //            }
        //            else
        //                mult = 10;

        //            if (dict.ContainsKey(mult))
        //                dict[mult] += 1;
        //            else
        //                dict.Add(mult, 1);
        //        }

        //        var sb = new StringBuilder();
        //        const int bet = 1;
        //        int payout = 0;
        //        foreach (var key in dict.Keys.OrderByDescending(x => x))
        //        {
        //            sb.AppendLine($"x{key} occured {dict[key]} times. {dict[key] * 1.0f / tests * 100}%");
        //            payout += key * dict[key];
        //        }
        //        try
        //        {
        //            await Context.Channel.SendConfirmAsync("BetRoll Test Results", sb.ToString(),
        //                footer: $"Total Bet: {tests * bet} | Payout: {payout * bet} | {payout * 1.0f / tests * 100}%");
        //        }
        //        catch { }

        //    });
        //    return Task.CompletedTask;
        //}

        [NadekoCommand, Usage, Description, Aliases]
        public async Task BetRoll(long amount)
        {
            if (amount < 1)
                return;

            long userFlowers;
            using (var uow = DbHandler.UnitOfWork())
            {
                userFlowers = uow.Currency.GetOrCreate(Context.User.Id).Amount;
            }

            if (userFlowers < amount)
            {
                await Context.Channel.SendErrorAsync($"{Context.User.Mention} You don't have enough {CurrencyPluralName}. You only have {userFlowers}{CurrencySign}.").ConfigureAwait(false);
                return;
            }

            await CurrencyHandler.RemoveCurrencyAsync(Context.User, "Betroll Gamble", amount, false).ConfigureAwait(false);

            var rng = new NadekoRandom().Next(0, 101);
            var str = $"{Context.User.Mention} `You rolled {rng}.` ";
            if (rng < 67)
            {
                str += "Better luck next time.";
            }
            else if (rng < 91)
            {
                str += $"Congratulations! You won {amount * NadekoBot.BotConfig.Betroll67Multiplier}{CurrencySign} for rolling above 66";
                await CurrencyHandler.AddCurrencyAsync(Context.User, "Betroll Gamble", (int)(amount * NadekoBot.BotConfig.Betroll67Multiplier), false).ConfigureAwait(false);
            }
            else if (rng < 100)
            {
                str += $"Congratulations! You won {amount * NadekoBot.BotConfig.Betroll91Multiplier}{CurrencySign} for rolling above 90.";
                await CurrencyHandler.AddCurrencyAsync(Context.User, "Betroll Gamble", (int)(amount * NadekoBot.BotConfig.Betroll91Multiplier), false).ConfigureAwait(false);
            }
            else
            {
                str += $"👑 Congratulations! You won {amount * NadekoBot.BotConfig.Betroll100Multiplier}{CurrencySign} for rolling **100**. 👑";
                await CurrencyHandler.AddCurrencyAsync(Context.User, "Betroll Gamble", (int)(amount * NadekoBot.BotConfig.Betroll100Multiplier), false).ConfigureAwait(false);
            }

            await Context.Channel.SendConfirmAsync(str).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Leaderboard()
        {
            var richest = new List<Currency>();
            using (var uow = DbHandler.UnitOfWork())
            {
                richest = uow.Currency.GetTopRichest(9).ToList();
            }
            if (!richest.Any())
                return;


            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(NadekoBot.BotConfig.CurrencySign + " Leaderboard");

            for (var i = 0; i < richest.Count; i++)
            {
                var x = richest[i];
                var usr = await Context.Guild.GetUserAsync(x.UserId).ConfigureAwait(false);
                var usrStr = "";
                if (usr == null)
                    usrStr = x.UserId.ToString();
                else
                    usrStr = usr.Username?.TrimTo(20, true);

                embed.AddField(efb => efb.WithName("#" + (i + 1) + " " + usrStr).WithValue(x.Amount.ToString() + " " + NadekoBot.BotConfig.CurrencySign).WithIsInline(true));
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}
