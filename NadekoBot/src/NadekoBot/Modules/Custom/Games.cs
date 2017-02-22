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

namespace NadekoBot.Modules.Games
{
    [NadekoModule("Games", ">")]

    public partial class Custom : DiscordModule
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task BetCard(int amount, string guess, [Remainder] string label = null)
        {
            var guessStr = guess.Trim().ToUpperInvariant();
            if (guessStr != "H"
                && guessStr != "S"
                && guessStr != "C"
                && guessStr != "D"
                && guessStr != "HEARTS"
                && guessStr != "SPADES"
                && guessStr != "CLUBS"
                && guessStr != "DIAMONDS")
                return;

            var labelStr = "";
            if (!string.IsNullOrWhiteSpace(label))
                labelStr = label.Trim().ToUpperInvariant();

            if (amount < NadekoBot.BotConfig.MinimumBetAmount)
            {
                await Context.Channel.SendErrorAsync($"You can't bet less than {NadekoBot.BotConfig.MinimumBetAmount}{NadekoBot.BotConfig.CurrencySign}.")
                             .ConfigureAwait(false);
                return;
            }

            long userFlowers;
            using (var uow = DbHandler.UnitOfWork())
            {
                userFlowers = uow.Currency.GetOrCreate(Context.User.Id).Amount;
            }

            if (userFlowers < amount)
            {
                await Context.Channel
                    .SendErrorAsync($"{Context.User.Mention} You don't have enough {NadekoBot.BotConfig.CurrencyPluralName}. You only have {userFlowers}{NadekoBot.BotConfig.CurrencySign}.").ConfigureAwait(false);
                return;
            }
            
            var debug_msg = new StringBuilder();
            var display_msg = new StringBuilder();
            var error_message = new StringBuilder();
            var file = "";          //  file to upload
            var card_label = "";    //  the label on the card
            var card_text = "";     //  the name of the card per order
            var type = "";          //  the type of drawn card
            var type_id = -1;       //  its id within the list
            var right_guess = -1;
            var guess_success_multiplier = 1.95;

            //! IDs of Card Types:
            //  0 - HEARTS   - color
            //  1 - SPADES   - bw
            //  2 - CLUBS    - bw
            //  3 - DIAMONDS - color
            var deck_of_cards = new List<List<string>>();
            for(var i = 0; i < 4; i++)
                deck_of_cards.Add(new List<string>());

            //  Labels:
            //  0 - ACE
            //  1
            //  2
            //  3
            //  4
            //  5
            //  6
            //  7
            //  8
            //  9
            //  J - Jack
            //  Q - Queen
            //  K - King
            var labels = new Dictionary<string, string> {{"ace","0"},{"one","1"},{"two","2"},{"three","3"},{"four","4"},
                { "five","5"},{"six","6"},{"seven","7"},{"eight","8"},{"nine","9"},{"jack","j"},{"queen","q"},{"king","k"}};

            try
            {
                var cards = Directory.GetFiles("data/images/cards").ToList();
                foreach (var card in cards)
                {
                    var path = card.Trim();
                    var text = path.ToUpperInvariant();

                    if (text.IndexOf("HEARTS") != -1)
                        deck_of_cards[0].Add(path);
                    else if (text.IndexOf("SPADES") != -1)
                        deck_of_cards[1].Add(path);
                    else if (text.IndexOf("CLUBS") != -1)
                        deck_of_cards[2].Add(path);
                    else if (text.IndexOf("DIAMONDS") != -1)
                       deck_of_cards[3].Add(path);
                }
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Reading the Image Files\n");
            }

            /*debug_msg.AppendLine($"Hearts: {deck_of_cards[0].Count()}");
            debug_msg.AppendLine($"Spades: {deck_of_cards[1].Count()}");
            debug_msg.AppendLine($"Clubs: {deck_of_cards[2].Count()}");
            debug_msg.AppendLine($"Diamonds: {deck_of_cards[3].Count()}");*/

            try
            {
                type_id = new NadekoRandom().Next(0, 8);
                var new_type_id = type_id - 4;
                if (new_type_id > 0)
                    type_id = new_type_id;
                var rand = new NadekoRandom().Next(0, deck_of_cards[type_id].Count());
                /*await Context.Channel.EmbedAsync(new EmbedBuilder()
                    .WithDescription($"Type: {type_id}\nID: {rand}\nCount: {deck_of_cards.Count()}\nList: {deck_of_cards[type_id].Count()}")).ConfigureAwait(false);*/
                file = deck_of_cards[type_id][rand];
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Extracting Type ID of file\n");
            }

            if (type_id == 0) type = "Heart";
            else if (type_id == 1) type = "Spade";
            else if (type_id == 2) type = "Club";
            else if (type_id == 3) type = "Diamond";

            //  remove the amount placed on bet
            await CurrencyHandler.RemoveCurrencyAsync(Context.User, "Betcard Gamble", amount, false).ConfigureAwait(false);

            if (guessStr == "HEARTS" || guessStr == "H")
                right_guess = 0;
            else if (guessStr == "SPADES" || guessStr == "S")
                right_guess = 1;
            else if (guessStr == "CLUBS" || guessStr == "C")
                right_guess = 2;
            else if (guessStr == "DIAMONDS" || guessStr == "D")
                right_guess = 3;

            try
            {
                var card_name = file.Substring(file.IndexOf("cards\\") + 6);
                card_label = card_name.Substring(0, card_name.IndexOf("_of"));
                //debug_msg.AppendLine(card_name);
                //debug_msg.AppendLine(card_label);
                card_text = labels[card_label].ToUpperInvariant();
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Getting the card label\n");
            }

            var success = false;
            var toWin = 0;
            if (!string.IsNullOrWhiteSpace(labelStr))
            {
                if ((right_guess == type_id) && (labelStr == card_text))
                {
                    toWin = (int)Math.Round(amount * guess_success_multiplier);
                    success = true;
                }
            }
            else
            {
                if (right_guess == type_id)
                {
                    toWin = (int)Math.Round(amount * guess_success_multiplier);
                    success = true;
                }
            }

            if (success)
            {
                display_msg.AppendLine($"{Context.User.Mention}`You drew a {type} card!` You won {toWin}{NadekoBot.BotConfig.CurrencySign}");
                await CurrencyHandler.AddCurrencyAsync(Context.User, "Betcard Gamble", toWin, false).ConfigureAwait(false);
            }
            else
                display_msg.AppendLine($"{Context.User.Mention}`You drew a {type} card of {card_label.ToUpperInvariant()}!` Better luck next time.");

            try
            {
                var sent = await Context.Channel.SendFileAsync(
                                File.Open(file, FileMode.OpenOrCreate),
                                new FileInfo(file).Name, display_msg.ToString())
                                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Uploading card file\n");
            }

            if (!string.IsNullOrWhiteSpace(debug_msg.ToString()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Debug Report").WithDescription($"{debug_msg.ToString()}")).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
        }
    }
}
