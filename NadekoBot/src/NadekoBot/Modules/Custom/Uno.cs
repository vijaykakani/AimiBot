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

namespace NadekoBot.Modules.Custom
{
    public class UnoCard
    {
        private int _cardID = 0;

        private string _label = "";
        private string _type = "";
        private string _color = "";
        private string _imagePath = "";

        private bool _cardTaken = false;
        private bool _cardPlaced = false;

        private bool _isWild = false;
        private bool _isPlus4 = false;
        private bool _isPlus2 = false;
        private bool _isSkip = false;
        private bool _isReverse = false;
        private bool _isNumber = false;

        public int ID() { return _cardID; }
        public string Label() { return _label; }
        public string Type() { return _type; }
        public string Color() { return _color; }
        public string ImagePath() { return _imagePath; }

        public bool IsCardTaken() { return _cardTaken; }
        public bool IsCardPlaced() { return _cardPlaced; }

        public void SetID(int val) { _cardID = val; }
        public void SetLabel(string val) { _label = val; }
        public void SetType(string val) { _type = val; }
        public void SetColor(string val) { _color = val; }
        public void SetImagePath(string val) { _imagePath = val; }

        public void SetCardTaken(bool val) { _cardTaken = val; }
        public void SetCardPlaced(bool val) { _cardPlaced = val; }

        public void SetWild(bool val) { _isWild = val; }
        public void SetPlus4(bool val) { _isPlus4 = val; }
        public void SetPlus2(bool val) { _isPlus2 = val; }
        public void SetSkip(bool val) { _isSkip = val; }
        public void SetReverse(bool val) { _isReverse = val; }
        public void SetNumber(bool val) { _isNumber = val; }

        public UnoCard(string label, string type, string color, string imagePath)
        {
            _label = label;
            _type = type;
            _color = color;
            _imagePath = imagePath;
        }

        public bool CanUse()
        {
            if (!_cardPlaced && !_cardTaken)
                return true;
            else
                return false;
        }

        public async void SendInfo(IUser usr)
        {
            var str = new StringBuilder();
            str.AppendLine("ID: " + _cardID);
            str.AppendLine("Label: " + _label.ToUpperInvariant());
            await usr.SendFileAsync(
                                File.Open(_imagePath, FileMode.OpenOrCreate),
                                new FileInfo(_imagePath).Name, str.ToString())
                                    .ConfigureAwait(false);
        }

        public void Reset()
        {
            _cardID = 0;
            _cardPlaced = false;
            _cardTaken = false;
        }
    }

    public class UnoPlayer
    {
        private IUser _usr;
        private List<UnoCard> _cards;
        private UnoGame _game;

        private bool _firstUnoCalled = false;
        private bool _finishedUno = false;
        private int _finishedUnoOrder = -1;

        private bool _alreadyDrawn = false;

        public IUser User() { return _usr; }
        public List<UnoCard> Cards() { return _cards; }

        public bool FirstUnoCalled() { return _firstUnoCalled; }
        public bool FinishedUno() { return _finishedUno; }
        public int FinishedUnoOrder() { return _finishedUnoOrder; }
        public bool AlreadyDrawn() { return _alreadyDrawn; }

        public UnoPlayer(IUser usr, List<UnoCard> cards, UnoGame game)
        {
            _usr = usr;
            _cards = cards;
            _game = game;
        }

        public void Take()
        {
            foreach (var card in _game.Deck())
            {
                if (card.CanUse())
                {
                    card.SetID(_cards.Count() + 1);
                    card.SetCardTaken(true);
                    _cards.Add(card);
                    break;
                }
            }

            _alreadyDrawn = true;
        }

        public void Reset()
        {
            _alreadyDrawn = false;
            _firstUnoCalled = false;
            _finishedUno = false;
            _finishedUnoOrder = -1;
        }
    }

    public class UnoGame
    {
        private List<UnoPlayer> _players;
        private List<UnoCard> _deck;
        private UnoCard _lastPlacedCard;
        private IChannel _startedChannel;

        private UnoPlayer _currentPlayer;
        private UnoPlayer _nextPlayer;
        private UnoPlayer _prevPlayer;
        private int _currentIndex = 0;

        private bool _gameRunning = false;
        private bool _roundEnded = false;

        public List<UnoPlayer> Players() { return _players; }
        public List<UnoCard> Deck() { return _deck; }
        public void SetDeck(List<UnoCard> cards) { _deck.Clear(); _deck = cards; }

        public UnoCard LastPlacedCard() { return _lastPlacedCard; }
        public void SetLastPlacedCard(UnoCard card) { _lastPlacedCard = card; }

        public IChannel StartedChannel() { return _startedChannel; }
        public void SetStartedChannel(IChannel ch) { _startedChannel = ch; }

        public bool IsGameRunning() { return _gameRunning; }
        public void SetGameRunning(bool val) { _gameRunning = val; }

        public bool RoundEnded() { return _roundEnded; }
        public void SetRoundEnded(bool val) { _roundEnded = val; }

        public static List<UnoCard> LoadCards()
        {
            var debug_msg = new StringBuilder();
            var error_message = new StringBuilder();

            var labels = new Dictionary<string, string> {{"0","0"},{"1","1"},{"2","2"},{"3","3"},{"4","4"},
                { "5","5"},{"6","6"},{"7","7"},{"8","8"},{"9","9"},{"s","skip"},{"r","reverse"},{"p2","plus 2"},{"w", "wild"},{"p4","plus 4"}};
            var colors = new Dictionary<string, string> { { "r", "red" }, { "b", "blue" }, { "g", "green" }, { "y", "yellow" }, { "w", "any" }, { "p4", "any" } };

            var to_path = "data/images/uno_cards";
            var files = Directory.GetFiles(to_path).ToList();
            var cards = new List<UnoCard>();

            foreach (var file in files)
            {
                var path = file.Trim();
                var file_name = path.Substring(0, path.IndexOf(".jpg")).Substring(path.IndexOf("uno_cards\\") + 10);
                var parts_of_name = file_name.Split(new string[] { "_" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                var label = "";
                var type = "";
                var color = colors[parts_of_name[1]];
                var multiply = 1;

                if (parts_of_name.Count() == 2)         //  Wild or Plus 4
                {
                    type = parts_of_name[1];
                    label = labels[type];
                    multiply = 4;
                }
                else                                    //  number, skip, reverse, plus 2
                {
                    type = parts_of_name[2];
                    label = labels[type];
                    if (label == "0")
                        multiply = 1;
                    else
                        multiply = 2;
                }

                for (var i = 0; i < multiply; i++)
                {
                    var card = new UnoCard(label, type, color, path);
                    switch (type)
                    {
                        case "w":
                            card.SetWild(true);
                            break;

                        case "p4":
                            card.SetPlus4(true);
                            break;

                        case "p2":
                            card.SetPlus2(true);
                            break;

                        case "s":
                            card.SetSkip(true);
                            break;

                        case "r":
                            card.SetReverse(true);
                            break;

                        default:
                            card.SetNumber(true);
                            break;
                    }

                    cards.Add(card);
                }
            }

            return cards;
        }

        public void Start()
        {
            _gameRunning = true;

            _players = new List<UnoPlayer>();
            _deck = new List<UnoCard>();

            ShuffleDeck();
        }

        public void HandCardsOut(IUser usr)
        {
            var id = 0;
            var cards = new List<UnoCard>();

            foreach (var card in _deck)
            {
                if (card.CanUse())
                {
                    id++;
                    card.SetID(id);
                    card.SetCardTaken(true);
                    cards.Add(card);
                }

                if (id == 7) break; //  break since 7 cards limit has been reached
            }

            _players.Add(new UnoPlayer(usr, cards, this));
        }

        //  Ref: http://www.vcskicks.com/randomize_array.php
        public void ShuffleDeck()
        {
            List<UnoCard> randomList = new List<UnoCard>();

            Random r = new Random();
            int randomIndex = 0;
            while (_deck.Count() > 0)
            {
                randomIndex = r.Next(0, _deck.Count());
                randomList.Add(_deck[randomIndex]);
                _deck.RemoveAt(randomIndex);
            }

            _deck = randomList;
        }

        public void UpdateDeck()
        {
            var n = 0;
            foreach (var card in _deck)
                if (card.CanUse())
                    n++;

            if (n < 20)
            {
                foreach (var card in _deck)
                    if (card != _lastPlacedCard)
                        card.SetCardPlaced(false);
            }
        }

        public void Reset()
        {
            _players.Clear();
            _gameRunning = false;
            _startedChannel = null;

            _lastPlacedCard = null;
            _currentPlayer = null;
            _nextPlayer = null;
            _prevPlayer = null;

            foreach (var card in _deck)
                card.Reset();
        }

        public bool PlayerExists(IUser usr)
        {
            foreach (var plr in _players)
            {
                if (plr.User() == usr)
                    return true;
            }
            return false;
        }

        public void PlayerLeft(IUser usr)
        {
            foreach (var plr in _players)
            {
                if (plr.User() == usr)
                {
                    foreach (var card in plr.Cards())
                        card.SetCardTaken(false);

                    break;
                }
            }
        }

        public UnoPlayer GetPlayer(IUser usr)
        {
            foreach (var plr in _players)
            {
                if (plr.User() == usr)
                    return plr;
            }
            return null;
        }

        public List<UnoCard> GetPlayerCards(IUser usr)
        {
            foreach (var plr in _players)
            {
                if (plr.User() == usr)
                    return plr.Cards();
            }
            return null;
        }
    }

    public class UnoChannel
    {
        private static List<UnoChannel> _channels;
        private UnoGame _game;

        public void SetGame(UnoGame game) { _game = game; }
        public UnoGame GetGame(IChannel ch)
        {
            UnoGame game = null;
            foreach (var chan in _channels)
            {
                if (chan == ch)
                    game = chan._game;
            }
            return game;
        }
    }

    [NadekoModule("Uno", "u!")]
    public partial class Uno : DiscordModule
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Start()
        {//!< TODO
            var display_msg = new StringBuilder();



            if (UnoGame.IsGameRunning())
                if (Context.Channel == UnoGame.StartedChannel())
                    display_msg.AppendLine($"The game is already running.");
            else
            {
                UnoCard.LoadCards();
                UnoGame.Start();
                UnoGame.HandCardsOut(Context.User);
                UnoCard placementCard = UnoCard.GetRandomCard();
                
                await Context.Channel.SendFileAsync(
                                File.Open(placementCard.ImagePath(), FileMode.OpenOrCreate),
                                new FileInfo(placementCard.ImagePath()).Name, "First Card Placed!")
                                    .ConfigureAwait(false);
                
                UnoGame.SetLastPlacedCard(placementCard);
                UnoGame.SetStartedChannel(Context.Channel);
                
                display_msg.AppendLine($"@everyone {Context.User.Mention} started Uno!");
            }
            
            if (!string.IsNullOrWhiteSpace(display_msg.ToString()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stop()
        {//!< TODO
            if (!UnoGame.IsGameRunning()) return;
            if (Context.Channel != UnoGame.StartedChannel()) return;

                UnoGame.Reset();
            
            var display_msg = new StringBuilder();
            display_msg.AppendLine($"Game stopped.");
            
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Shuffle()
        {//!< TODO
            if (!UnoGame.IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("Cannot shuffle deck when the game isn't running.")).ConfigureAwait(false);
            else
            {
                if (Context.Channel != UnoGame.StartedChannel()) return;

                UnoGame.UpdateDeck();
                UnoGame.ShuffleDeck();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("Shuffled the deck.")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Join()
        {//!< TODO
            var display_msg = new StringBuilder();
            if (!UnoGame.IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("Cannot join when the game isn't running.")).ConfigureAwait(false);
            else
            {
                if (Context.Channel != UnoGame.StartedChannel()) return;
                if (!UnoGame.PlayerExists(Context.User))
                    UnoGame.HandCardsOut(Context.User);
            }
        }
        
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Place()
        {//!< TODO
            if (!UnoGame.IsGameRunning())
                return;

            if (Context.Channel != UnoGame.StartedChannel()) return;

            var display_msg = new StringBuilder();
            display_msg.AppendLine($"-- UNDER CONSTRUCTION --");
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Take()
        {//!< TODO
            if (!UnoGame.IsGameRunning()) return;

            var display_msg = new StringBuilder();

            if (UnoGame.PlayerExists(Context.User))
            {
                var plr = UnoGame.GetPlayer(Context.User);
                if (!plr.AlreadyDrawn())
                {
                    plr.Take();
                    display_msg.AppendLine($"{Context.User.Mention} drew a card.");
                }
                else
                    display_msg.AppendLine($"{Context.User.Mention} You already drew this turn.");
            }
            else
                display_msg.AppendLine($"{Context.User.Mention} You must be playing Uno to draw a card.");

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Hand()
        {//!< TODO
            if (!UnoGame.IsGameRunning()) return;

            var display_msg = new StringBuilder();
            if (UnoGame.PlayerExists(Context.User))
            {
                var plr_cards = UnoGame.GetPlayerCards(Context.User);
                foreach (var card in plr_cards)
                    card.SendInfo(Context.User);
            }
            else
            {
                display_msg.AppendLine($"{Context.User.Mention} You don't have cards because you are not playing Uno!");
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
            }
        }
        
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Unos()
        {//!< TODO
            if (!UnoGame.IsGameRunning()) return;

            var display_msg = new StringBuilder();
            display_msg.AppendLine($"-- UNDER CONSTRUCTION --");
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Quit()
        {//!< TODO
            if (!UnoGame.IsGameRunning()) return;

            if (UnoGame.PlayerExists(Context.User))
            {
                UnoGame.PlayerLeft(Context.User);
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription($"{Context.User.Mention} left the game.")).ConfigureAwait(false);
                

            }
        }
    }
}