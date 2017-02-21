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

        public bool IsWild() { return _isWild; }
        public bool IsPlus4() { return _isPlus4; }
        public bool IsPlus2() { return _isPlus2; }
        public bool IsSkip() { return _isSkip; }
        public bool IsReverse() { return _isReverse; }
        public bool IsNumber() { return _isNumber; }

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
            str.AppendLine("`ID:` " + _cardID);
            str.AppendLine("`Label:` " + _label.ToUpperInvariant() + " Card");
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
        private IUser _user;
        private List<UnoCard> _cards = new List<UnoCard>();
        private UnoGame _game;
        private IMessageChannel _channel;

        private bool _firstUnoCalled = false;
        private bool _finishedUno = false;
        private int _finishedUnoOrder = -1;

        private bool _alreadyDrawn = false;

        public IUser User() { return _user; }
        public List<UnoCard> Cards() { return _cards; }

        public bool FirstUnoCalled() { return _firstUnoCalled; }
        public bool FinishedUno() { return _finishedUno; }
        public int FinishedUnoOrder() { return _finishedUnoOrder; }
        public void SetFirstUnoCalled(bool val) { _firstUnoCalled = val; }
        public void SetFinishedUno(bool val) { _finishedUno = val; }
        public void SetFinishedUnoOrder(int val) { _finishedUnoOrder = val; }

        public bool AlreadyDrawn() { return _alreadyDrawn; }
        public void SetAlreadyDrawn(bool val) { _alreadyDrawn = val; }

        public UnoPlayer(IUser usr, List<UnoCard> cards, UnoGame game, IMessageChannel ch)
        {
            _user = usr;
            _cards = cards;
            _game = game;
            _channel = ch;
        }

        public void UpdateCards()
        {
            var card_id = 0;
            foreach (var card in _cards)
            {
                card_id++;
                card.SetID(card_id);
            }
        }

        public void ShowCards()
        {
            foreach (var card in _cards)
                card.SendInfo(_user);
        }

        public async void Draw()
        {
            _game.UpdateDeck();

            await _user.SendConfirmAsync("**---- You Drew This UNO Card ----**").ConfigureAwait(false);

            foreach (var card in _game.Deck())
            {
                if (card.CanUse())
                {
                    card.SetID(_cards.Count() + 1);
                    card.SetCardTaken(true);
                    card.SendInfo(_user);
                    _cards.Add(card);
                    break;
                }
            }

            _alreadyDrawn = true;
        }

        public void RemoveCard(UnoCard card)
        {
            card.SetID(0);
            card.SetCardTaken(false);
            _cards.Remove(card);
        }

        public void RemoveCards()
        {
            foreach (var card in _cards)
                RemoveCard(card);
        }

        public void Leave()
        {
            RemoveCards();
            _game.Players().Remove(this);
        }
    }

    public class UnoGame
    {
        private List<UnoPlayer> _players = new List<UnoPlayer>();
        private List<UnoCard> _deck = new List<UnoCard>();
        private UnoCard _lastPlacedCard;
        private IMessageChannel _channel;

        private UnoPlayer _currentPlayer;
        private int _currentPlayerIndex = 0;
        private int _startingPlayerIndex = 0;
        private int _playersFinishedOrder = 0;

        private bool _gameRunning = false;
        private bool _isGameReversed = false;

        public List<UnoPlayer> Players() { return _players; }
        public List<UnoCard> Deck() { return _deck; }
        public void SetDeck(List<UnoCard> cards) { _deck.Clear(); _deck = cards; }

        public UnoCard LastPlacedCard() { return _lastPlacedCard; }
        public void SetLastPlacedCard(UnoCard card) { _lastPlacedCard = card; }

        public IMessageChannel Channel() { return _channel; }
        public void SetChannel(IMessageChannel ch) { _channel = ch; }

        public bool IsGameRunning() { return _gameRunning; }
        public void SetGameRunning(bool val) { _gameRunning = val; }

        public bool IsGemeReversed() { return _isGameReversed; }
        public void SetGameReversed(bool val) { _isGameReversed = val; }

        public UnoPlayer CurrentPlayer() { return _currentPlayer; }
        public int CurrentPlayerIndex() { return _currentPlayerIndex; }
        public void SetCurrentPlayer(IUser usr)
        {
            foreach (var plr in _players)
            {
                if (plr.User() == usr)
                    SetCurrentPlayer(plr);
            }
        }
        public void SetCurrentPlayer(UnoPlayer plr) { _currentPlayer = plr; }
        public void SetCurrentPlayerIndex(int index) { _currentPlayerIndex = index; }

        public void SetStartingPlayerIndex(int index) { _startingPlayerIndex = index; }
        public int GetStartingPlayerIndex() { return _startingPlayerIndex; }

        public void SetPlayersFinishedOrder(int val) { _playersFinishedOrder = val; }
        public void IncreasePlayersFinishedOrder() { _playersFinishedOrder += 1; }
        public void DecreasePlayersFinishedOrder() { _playersFinishedOrder -= 1; }
        public int PlayersFinishedOrder() { return _playersFinishedOrder; }

        public UnoPlayer GetNextPlayer()
        {
            int player_index = 0;
            if (!_isGameReversed)
                SkipToPlayer(_currentPlayerIndex, 0, true, ref player_index);
            else
                SkipToPlayer(_currentPlayerIndex, 0, false, ref player_index);
            return _players[player_index];
        }

        public UnoPlayer GetPreviousPlayer()
        {
            int player_index = 0;
            if (!_isGameReversed)
                SkipToPlayer(_currentPlayerIndex, 0, false, ref player_index);
            else
                SkipToPlayer(_currentPlayerIndex, 0, true, ref player_index);
            return _players[player_index];
        }

        public UnoPlayer SkipToPlayer(int current_index, int skip_times, bool positive, ref int player_index)
        {
            if (positive)
            {
                //  Example:
                //  skip times = 1
                //  total players = 4
                //  current player index = 3
                //  next player index = (3 + 1) + 1 = 5 - (total players) = 1;

                var next_player_index = current_index + 1 + skip_times;
                while (next_player_index >= _players.Count())
                {
                    next_player_index = next_player_index - _players.Count();
                }
                player_index = next_player_index;
                return _players[next_player_index];
            }
            else
            {
                //  Example:
                //  skip times = 3
                //  total players = 4
                //  current player index = 3
                //  previous player index = (3 - 1) - 3 = (total players) - 1 = 3;

                var previous_player_index = current_index - 1 + skip_times;
                while (previous_player_index < 0)
                {
                    previous_player_index = _players.Count() + previous_player_index;
                }
                player_index = previous_player_index;
                return _players[previous_player_index];
            }
        }

        public void LoadCards()
        {
            if (_deck.Count() > 0) return;

            var labels = new Dictionary<string, string> {{"0","0"},{"1","1"},{"2","2"},{"3","3"},{"4","4"},
                { "5","5"},{"6","6"},{"7","7"},{"8","8"},{"9","9"},{"s","skip"},{"r","reverse"},{"p2","plus 2"},{"w", "wild"},{"p4","plus 4"}};
            var colors = new Dictionary<string, string> { { "r", "red" }, { "b", "blue" }, { "g", "green" }, { "y", "yellow" }, { "w", "any" }, { "p4", "any" } };

            var to_path = "data/images/uno_cards";
            var files = Directory.GetFiles(to_path).ToList();

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

                    _deck.Add(card);
                }
            }
        }

        public void Start()
        {
            _gameRunning = true;

            _players = new List<UnoPlayer>();

            LoadCards();
            ShuffleDeck();
        }

        public async void Update()
        {
            if (NoPlayers())
            {
                await _channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"All players have left the game. Uno is stopped")).ConfigureAwait(false);
                Reset();
            }
            else
            {
                UpdateDeck();

                //  A full round is complete, so this can be enabled again
                if (_startingPlayerIndex == _currentPlayerIndex)
                {
                    foreach (var plr in _players)
                        plr.SetAlreadyDrawn(false);
                }
            }
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

            _players.Add(new UnoPlayer(usr, cards, this, this._channel));
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

        public async void UpdateDeck()
        {
            var n = 0;
            foreach (var card in _deck)
                if (card.CanUse())
                    n++;

            if (n < 15)
            {
                foreach (var card in _deck)
                    if (card != _lastPlacedCard)
                        if (!card.IsCardTaken())
                            card.SetCardPlaced(false);

                ShuffleDeck();

                await _channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("Deck is updated!")).ConfigureAwait(false);
            }
        }

        public UnoCard GetRandomCard()
        {
            Random rnd = new Random();
            int r = rnd.Next(_deck.Count);
            return _deck[r];
        }

        public void Reset()
        {
            _players.Clear();
            _gameRunning = false;
            _channel = null;

            _lastPlacedCard = null;
            _currentPlayer = null;

            _currentPlayerIndex = 0;
            _startingPlayerIndex = 0;

            foreach (var card in _deck)
                card.Reset();

            UnoChannel ch = UnoChannel.GetGameChannel(_channel);
            ch.RemoveGame();
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

        public bool NoPlayers()
        {
            if (_players.Count() > 0) return false;
            return true;
        }
    }

    public class UnoChannel
    {
        private static List<UnoChannel> _channels = new List<UnoChannel>();
        private IMessageChannel _channel;
        private UnoGame _game;

        public void SetGame (UnoGame game) { _game = game; }
        public UnoGame Game() { return _game; }

        public static UnoChannel GetGameChannel(IMessageChannel ch)
        {
            foreach (var chan in _channels)
            {
                if (chan._channel == ch)
                    return chan;
            }
            return null;
        }

        public static bool DoesGameChannelExist(IMessageChannel ch)
        {
            foreach (var chan in _channels)
            {
                if (chan._channel == ch)
                    return true;
            }
            return false;
        }

        public static string GetDaySuffix(int day)
        {
            switch (day)
            {
                case 1:
                case 21:
                case 31:
                    return "st";
                case 2:
                case 22:
                    return "nd";
                case 3:
                case 23:
                    return "rd";
                default:
                    return "th";
            }
        }

        public UnoChannel(IMessageChannel ch)
        {
            _channel = ch;

            _game = new UnoGame();
            _game.SetChannel(ch);

            _channels.Add(this);
        }

        public void RemoveGame()
        {
            _game = null;
            _channels.Remove(this);
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
            var error_message = new StringBuilder();

            UnoChannel GameChannel = null;
            try
            {
                if (!UnoChannel.DoesGameChannelExist(Context.Channel))
                    GameChannel = new UnoChannel(Context.Channel);
                else
                    GameChannel = UnoChannel.GetGameChannel(Context.Channel);
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Creating new game\n");
            }
            
            try
            {
                if (GameChannel.Game().IsGameRunning())
                    display_msg.AppendLine($"The game is already running");
                else
                {
                    try
                    {
                        GameChannel.Game().Start();
                        GameChannel.Game().HandCardsOut(Context.User);
                    }
                    catch (Exception ex)
                    {
                        error_message.AppendLine($"Message: {ex.Message}\nSource: Starting game\n");
                    }

                    try
                    {
                        UnoCard placementCard = GameChannel.Game().GetRandomCard();
                        while (placementCard.IsWild() || placementCard.IsPlus4() || placementCard.IsPlus2() || placementCard.IsSkip() || placementCard.IsReverse())
                            placementCard = GameChannel.Game().GetRandomCard();
                        placementCard.SetCardPlaced(true);

                        await Context.Channel.SendFileAsync(
                                        File.Open(placementCard.ImagePath(), FileMode.OpenOrCreate),
                                        new FileInfo(placementCard.ImagePath()).Name, "First Card Placed!")
                                            .ConfigureAwait(false);

                        GameChannel.Game().SetLastPlacedCard(placementCard);
                        GameChannel.Game().SetCurrentPlayer(Context.User);
                        GameChannel.Game().SetCurrentPlayerIndex(0);
                    }
                    catch (Exception ex)
                    {
                        error_message.AppendLine($"Message: {ex.Message}\nSource: Placing the first card\n");
                    }

                    display_msg.AppendLine($"@everyone {Context.User.Mention} started Uno!");
                }
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Running the start\n");
            }
            
            if (!string.IsNullOrWhiteSpace(display_msg.ToString()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stop()
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);

            var display_msg = new StringBuilder();
            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                display_msg.AppendLine($"The game is not running");
            else
            {
                GameChannel.Game().Reset();
                display_msg.AppendLine($"Game stopped");
            }
            
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Shuffle()
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);

            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("Cannot shuffle deck when the game isn't running")).ConfigureAwait(false);
            else
            {
                GameChannel.Game().ShuffleDeck();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("Shuffled the deck")).ConfigureAwait(false);
                GameChannel.Game().UpdateDeck();
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task NextPlayer()
        {//!< TODO
            var display_msg = new StringBuilder();
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);

            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot see next player when the game is not running")).ConfigureAwait(false);
            else
            {
                UnoPlayer plr = GameChannel.Game().GetNextPlayer();
                var mention = "";
                if (plr.User() == Context.User)
                    mention = "you";
                else
                    mention = plr.User().Mention;

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} The next player is {mention}")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task PreviousPlayer()
        {//!< TODO
            var display_msg = new StringBuilder();
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);

            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot see next player when the game is not running")).ConfigureAwait(false);
            else
            {
                UnoPlayer plr = GameChannel.Game().GetPreviousPlayer();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} The previous player is {plr.User().Mention}")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Join()
        {//!< TODO
            var display_msg = new StringBuilder();
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);

            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot join when the game is not running. To start the game, use: `u!start`")).ConfigureAwait(false);
            else
            {
                if (!GameChannel.Game().PlayerExists(Context.User))
                    GameChannel.Game().HandCardsOut(Context.User);
                else
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                        .WithDescription($"{Context.User.Mention} You are already playing")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Place([Remainder] string param = null)
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);
            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                return;

            if (string.IsNullOrWhiteSpace(param))
                return;

            if (!GameChannel.Game().PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} You aren't playing the game. To join, do: `u!join`")).ConfigureAwait(false);
                return;
            }

            var plr = GameChannel.Game().GetPlayer(Context.User);
            UnoPlayer new_current_player = null;
            int new_current_player_index = -1;

            if (plr != GameChannel.Game().CurrentPlayer())
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} Please wait. It isn't your turn to play yet")).ConfigureAwait(false);
                return;
            }
            
            var paramStr = param.Trim();
            var paramList = paramStr.Split(new string[] { " ", "," }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var display_msg = new StringBuilder();
            var error_msg = new StringBuilder();
            var success = false;
            var entry = 0;

            var cards = new List<UnoCard>();

            var Plus4 = false;
            var Plus2 = false;
            var Reverse = false;
            var Skip = false;
            var Wild = false;
            var Number = false;

            var no_wild = 0;
            var no_plus4 = 0;
            var no_plus2 = 0;
            var no_skip = 0;
            var no_reverse = 0;
            var no_number = 0;

            var processed_cards = new List<int>();

            foreach (var val in paramList)
            {
                var val_int = 0;
                entry++;
                if (int.TryParse(val.Trim(), out val_int))
                {
                    //  to ensure same ids cannot be input more than once
                    if (processed_cards.Contains(val_int))
                        continue;
                    else processed_cards.Add(val_int);
                        
                    if ((val_int > plr.Cards().Count()) || (val_int < 1))
                    {
                        error_msg.AppendLine($"Entry {entry}: {val} is not a valid digit. Use `u!hand` to use the proper ids of the cards you want to place");
                        cards.Clear();
                        break;
                    }
                    else
                    {
                        var card_id = val_int - 1;
                        UnoCard card = plr.Cards()[card_id];

                        if (card.IsWild()) { Wild = true; no_wild++; }
                        if (card.IsPlus4()) { Plus4 = true; no_plus4++; }
                        if (card.IsPlus2()) { Plus2 = true; no_plus2++; }
                        if (card.IsSkip()) { Skip = true; no_skip++; }
                        if (card.IsReverse()) { Reverse = true; no_reverse++; }
                        if (card.IsNumber()) { Number = true; no_number++; }

                        cards.Add(card);
                    }
                }
                else
                {
                    error_msg.AppendLine($"Entry {entry}: {val} is not a valid digit. Please input proper numbers");
                    cards.Clear();
                    break;
                }
            }

            if (cards.Count() > 0)
            {
                var card_counter = 0;
                var proceed = false;

                //  This is the process to check the correct order of the cards
                //  so that the wrong way of placing the cards is taken out
                if (Wild & Plus4)
                    error_msg.AppendLine($"You must have only one `1 Wild Card` or `1 Plus 4 Card` at a time");
                else if (Wild)
                {
                    if (no_wild > 1)
                        error_msg.AppendLine($"You can place only `1 Wild Card` at a time");
                    else
                    {
                        UnoCard card = cards[0];
                        if (!card.IsWild())
                            error_msg.AppendLine($"You need to place the `Wild Card` first in the order of entries for it to work");
                        else if (!Number)
                            error_msg.AppendLine($"A `Wild Card` must have a number card of any colour at the end of it");
                        else if (Number)
                        {
                            UnoCard last_card = cards[cards.Count() - 1];
                            if (!last_card.IsNumber())
                                error_msg.AppendLine($"The last card in the row starting with a `Wild Card` must be a number card");
                            else
                            {
                                var counter = 0;
                                foreach (var s_card in cards)
                                {
                                    if (s_card.IsNumber() && (last_card.Label() == s_card.Label()))
                                        counter++;
                                }

                                if (counter < no_number)
                                    error_msg.AppendLine($"{counter} / {no_number} `Number Cards` you placed do not match. Please ensure to match the labels of the cards");
                                else
                                {
                                    card_counter++;
                                    proceed = true;
                                }

                            }
                        }
                        else
                        {
                            card_counter++;
                            proceed = true;
                        }
                    }
                }
                else if (Plus4)
                {
                    if (no_plus4 > 1)
                        error_msg.AppendLine($"You can only place `1 Plus 4 Card` at a time");
                    else
                    {
                        UnoCard card = cards[0];
                        if (!card.IsWild())
                            error_msg.AppendLine($"You need to place the `Plus 4 Card` first in the order of entries for it to work");
                        else if (!Number)
                            error_msg.AppendLine($"A `Plus 4 Card` must have a number card of any colour at the end of it");
                        else if (Number)
                        {
                            UnoCard last_card = cards[cards.Count() - 1];
                            if (!last_card.IsNumber())
                                error_msg.AppendLine($"The last card in the row starting with a `Plus 4 Card` must be a number card");
                            else
                            {
                                var counter = 0;
                                foreach (var s_card in cards)
                                {
                                    if (s_card.IsNumber() && (last_card.Label() == s_card.Label()))
                                        counter++;
                                }

                                if (counter < no_number)
                                    error_msg.AppendLine($"{counter} / {no_number} `Number Cards` you placed do not match. Please ensure to match the labels of the cards");
                                else
                                {
                                    card_counter++;
                                    proceed = true;
                                }
                            }
                        }
                        else
                        {
                            card_counter++;
                            proceed = true;
                        }
                    }
                }
                else if (Plus2 && Skip && Reverse && Number)
                    error_msg.AppendLine($"You can only place `Plus 2 Card`, `Skip Card`, `Reverse Card` or `Number Card` at a time");
                else if (Plus2 && Skip && Reverse)
                    error_msg.AppendLine($"You can only place `Plus 2 Card`, `Skip Card` or `Reverse Card` at a time");
                else if (Plus2 && Skip && Number)
                    error_msg.AppendLine($"You can only place `Plus 2 Card`, `Skip Card` or `Number Card` at a time");
                else if (Plus2 && Reverse && Number)
                    error_msg.AppendLine($"You can only place `Plus 2 Card`, `Reverse Card` or `Number Card` at a time");
                else if (Plus2 && Skip)
                    error_msg.AppendLine($"You can only place `Plus 2 Card` or `Skip Card` at a time");
                else if (Plus2 && Reverse)
                    error_msg.AppendLine($"You can only place `Plus 2 Card` or `Reverse Card` at a time");
                else if (Plus2 && Number)
                    error_msg.AppendLine($"You can only place `Plus 2 Card` or `Number Card` at a time");
                else if (Skip && Reverse && Number)
                    error_msg.AppendLine($"You can only place `Skip Card`, `Reverse Card` or `Number Card` at a time");
                else if (Skip && Reverse)
                    error_msg.AppendLine($"You can only place `Skip Card` or `Reverse Card` at a time");
                else if (Skip && Number)
                    error_msg.AppendLine($"You can only place `Skip Card` or `Number Card` at a time");
                else if (Reverse && Number)
                    error_msg.AppendLine($"You can only place `Reverse Card` or `Number Card` at a time");
                else proceed = true;

                //  NUMBER CODE CHECK
                //  Processing when there's no Wild or Plus 4 or other Action Cards
                if (Number && proceed && !Wild && !Plus4 && !Plus2 && !Skip && !Reverse)
                {
                    var counter = 0;
                    UnoCard last_card = cards[cards.Count() - 1];
                    foreach (var card in cards)
                    {
                        if (card.IsNumber() && (last_card.Label() == card.Label()))
                            counter++;
                    }

                    if (counter < no_number)
                    {
                        error_msg.AppendLine($"{counter} / {no_number} `Number Cards` you placed do not match. Please ensure to match the labels of the cards");
                        proceed = false;
                    }
                    else
                    {
                        //  Example:
                        //  last placed card = R | 9
                        //  last select card = B | 9
                        //  Colours don't match, but the labels do
                        //  Result = SUCCESS

                        //  Example:
                        //  last placed card = R | 9
                        //  last select card = R | 7
                        //  Colours match, but the labels don't
                        //  Result = SUCCESS

                        //  last placed card = R | 9
                        //  last select card = B | 7
                        //  Colours don't match, and the labels don't match
                        //  Result = FAILURE

                        UnoCard first_card = cards[0];
                        if ((first_card.Label() != GameChannel.Game().LastPlacedCard().Label())
                            && (first_card.Color() != GameChannel.Game().LastPlacedCard().Color()))
                        {
                            error_msg.AppendLine($"Your first `{first_card.Label()} Number Card` does not match with the last card already played");
                            proceed = false;
                        }
                    }
                }

                //  SKIP CODE CHECK
                //  Processing when there's only Skip Action Card
                if (!Number && proceed && !Wild && !Plus4 && !Plus2 && Skip && !Reverse)
                {
                    UnoCard first_card = cards[0];
                    if ((first_card.Label() != GameChannel.Game().LastPlacedCard().Label())
                            && (first_card.Color() != GameChannel.Game().LastPlacedCard().Color()))
                    {
                        error_msg.AppendLine($"Your first `Skip Card` does not match in color with the last card already played");
                        proceed = false;
                    }
                }

                //  REVERSE CODE CHECK
                //  Processing when there's only Reverse Action Card
                if (!Number && proceed && !Wild && !Plus4 && !Plus2 && !Skip && Reverse)
                {
                    UnoCard first_card = cards[0];
                    if ((first_card.Label() != GameChannel.Game().LastPlacedCard().Label())
                            && (first_card.Color() != GameChannel.Game().LastPlacedCard().Color()))
                    {
                        error_msg.AppendLine($"Your first `Reverse Card` does not match with the last card already played");
                        proceed = false;
                    }
                }

                //  PLUS 2 CODE CHECK
                //  Processing when there's only Reverse Action Card
                if (!Number && proceed && !Wild && !Plus4 && Plus2 && !Skip && !Reverse)
                {
                    UnoCard first_card = cards[0];
                    if ((first_card.Label() != GameChannel.Game().LastPlacedCard().Label())
                            && (first_card.Color() != GameChannel.Game().LastPlacedCard().Color()))
                    {
                        error_msg.AppendLine($"Your first `Plus 2 Card` does not match with the last card already played");
                        proceed = false;
                    }
                }

                if (proceed)
                {
                    UnoCard last_card = cards[cards.Count() - 1];
                    var go_turns = 0;
                    var DrawCards = 0;

                    if (Plus4)
                        DrawCards += 4;

                    if (Plus2)
                        DrawCards += 2 * no_plus2;

                    if (Skip)
                        go_turns = no_skip;

                    //  CARD PROCESS PROCEDURE:
                    //  If Wild:
                    //      Followed by: Plus 2 Card and/or Skip Card and/or Reverse Card
                    //      Finished by: Matching Number Card
                    //
                    //  If Plus 4:
                    //      Followed by: Plus 2 Card and/or Skip Card and/or Reverse Card
                    //      Finished by: Matching Number Cards

                    if (card_counter == 1)  //  Wild/Plus 4 Card came first, then...
                    {
                        if (Skip || Reverse)
                            new_current_player_index = GameChannel.Game().CurrentPlayerIndex();

                        foreach (var card in cards)
                        {
                            if (card.IsSkip())
                            {
                                if (!GameChannel.Game().IsGemeReversed())
                                    new_current_player = GameChannel.Game().SkipToPlayer(new_current_player_index, 1, true, ref new_current_player_index);
                                else
                                    new_current_player = GameChannel.Game().SkipToPlayer(new_current_player_index, -1, false, ref new_current_player_index);
                            }

                            if (card.IsReverse())
                            {
                                if (GameChannel.Game().IsGemeReversed())
                                    GameChannel.Game().SetGameReversed(false);
                                else
                                    GameChannel.Game().SetGameReversed(true);

                                //  the effect goes to the person after the chosen one
                                if (!GameChannel.Game().IsGemeReversed())
                                    new_current_player = GameChannel.Game().SkipToPlayer(new_current_player_index, 0, true, ref new_current_player_index);
                                else
                                    new_current_player = GameChannel.Game().SkipToPlayer(new_current_player_index, 0, false, ref new_current_player_index);
                            }

                            card.SetCardPlaced(true);
                            plr.RemoveCard(card);
                        }
                    }

                    //  CARD PROCESS PROCEDURE:
                    //  PLUS 2 CARDS:
                    //              + Makes the next player draw 2 cards
                    //              + Must match with the colour of the last card placed
                    //
                    //  SKIP CARDS:
                    //              + Any Colour of multiples
                    //              + Must match the colour of the last card placed
                    //
                    //  REVERSE CARDS:
                    //              + Any Colour of multiples
                    //              + Must match the colour of the last card placed
                    //              + Double Reverse bring it back to you and sets the game running in original order
                    //
                    //  NUMBER CARDS:
                    //              + Any colour or label of multiples
                    //              + Must match either the colour or number of the last card placed
                    //              + If the last card is a skip, +2 or reverse, then the colour must match

                    else    //  No Wild/Plus 4 card detected, then...
                    {
                        if (Reverse)
                        {
                            foreach (var card in plr.Cards())
                            {
                                if (card.IsReverse())
                                {
                                    if (GameChannel.Game().IsGemeReversed())
                                        GameChannel.Game().SetGameReversed(false);
                                    else
                                        GameChannel.Game().SetGameReversed(true);
                                }
                            }
                        }
                    }

                    //  Set that all the cards selected to be placed
                    foreach (var card in cards)
                    {
                        card.SetCardPlaced(true);
                        plr.RemoveCard(card);
                    }

                    var file_msg = new StringBuilder();
                    if (card_counter == 1)
                        file_msg.AppendLine($"✋ {plr.User().Mention} placed {cards.Count()} cards");
                    else
                        file_msg.AppendLine($"✋ {plr.User().Mention} placed {cards.Count()} cards".ToString().SnPl(cards.Count()) + $" of » {last_card.Label()} «");

                    for (var i = 0; i < cards.Count(); i++)
                    {
                        if ((i + 1) == cards.Count())
                            file_msg.AppendLine($":arrow_forward: {cards[i].Label()}");
                        else
                            file_msg.AppendLine($":arrow_forward: {cards[i].Label()} 🔽");
                    }

                    await Context.Channel.SendFileAsync(
                                        File.Open(last_card.ImagePath(), FileMode.OpenOrCreate),
                                        new FileInfo(last_card.ImagePath()).Name, file_msg.ToString())
                                        .ConfigureAwait(false);

                    //  update the cards with the player by giving proper ids
                    plr.UpdateCards();

                    //  showing the names of all the players skipped.
                    if (Skip && (no_skip > 0))
                    {
                        display_msg.AppendLine($"➤ {plr.User().Mention} skipped {no_skip} turns".ToString().SnPl(no_skip));

                        for (var i = 0; i < no_skip; i++)
                        {
                            int next_player_index = 0;
                            UnoPlayer next_player = null;

                            if (!GameChannel.Game().IsGemeReversed())
                                next_player = GameChannel.Game().SkipToPlayer(GameChannel.Game().CurrentPlayerIndex(), i, true, ref next_player_index);
                            else
                                next_player = GameChannel.Game().SkipToPlayer(GameChannel.Game().CurrentPlayerIndex(), -i, false, ref next_player_index);
                            display_msg.AppendLine($"⏩ {next_player.User().Mention} got skipped");
                        }
                    }

                    //  sets the starting index for the new round
                    if (Reverse)
                    {
                        GameChannel.Game().SetStartingPlayerIndex(new_current_player_index);
                        display_msg.AppendLine($"➤ {plr.User().Mention} reversed {no_reverse} times".ToString().SnPl(no_reverse));
                    }

                    //  selecting the next player to continue the game from
                    if ((new_current_player_index == -1) && (new_current_player == null))
                    {
                        if (!GameChannel.Game().IsGemeReversed())
                            new_current_player = GameChannel.Game().SkipToPlayer(
                            GameChannel.Game().CurrentPlayerIndex(), go_turns, true, ref new_current_player_index);
                        else
                            new_current_player = GameChannel.Game().SkipToPlayer(
                            GameChannel.Game().CurrentPlayerIndex(), -go_turns, false, ref new_current_player_index);
                    }

                    for (int i = 0; i < DrawCards; i++)
                        new_current_player.Draw();

                    if (DrawCards > 0)
                        display_msg.AppendLine($":scream: {plr.User().Mention} made {new_current_player.User().Mention} draw {DrawCards} cards");

                    //  sets the new current player
                    GameChannel.Game().SetCurrentPlayer(GameChannel.Game().Players()[new_current_player_index]);
                    GameChannel.Game().SetCurrentPlayerIndex(new_current_player_index);
                    GameChannel.Game().SetLastPlacedCard(last_card);

                    if (plr.Cards().Count() == 1)
                    {
                        if (plr.FirstUnoCalled())
                        {
                            GameChannel.Game().IncreasePlayersFinishedOrder();
                            plr.SetFinishedUnoOrder(GameChannel.Game().PlayersFinishedOrder());
                            display_msg.AppendLine($"🎇 {plr.User().Mention} has finished UNO in {plr.FinishedUnoOrder()}{UnoChannel.GetDaySuffix(plr.FinishedUnoOrder())} place");
                        }
                        else
                        {
                            plr.SetFirstUnoCalled(true);
                            display_msg.AppendLine($"⛳ {plr.User().Mention} has called Uno with only one card in hand");
                        }
                    }
                    else if (plr.Cards().Count() == 0)
                    {
                        GameChannel.Game().IncreasePlayersFinishedOrder();
                        plr.SetFinishedUnoOrder(GameChannel.Game().PlayersFinishedOrder());
                    }

                    success = true;
                }
            }

            if (success)
            {
                display_msg.AppendLine($"\n👉 It is your turn {new_current_player.User().Mention} 👈");
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);

                GameChannel.Game().Update();
            }
            else
            {
                await plr.User().SendErrorAsync("❕ " + error_msg.ToString()).ConfigureAwait(false);

                if (plr.Cards().Count() == 1)
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                        .WithDescription($"❕ {plr.User().Mention} You don't have any matching cards to place. Type `u!draw` to draw a card`")).ConfigureAwait(false);
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Skip()
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);
            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning())
                return;

            //  Checks whether the player has either:
            //
            //  - placed a card
            //  or
            //  - drawn a card
            //  
            //  to be able to get the game to move onto the next player

            if (!GameChannel.Game().PlayerExists(Context.User))
                return;

            UnoPlayer plr = GameChannel.Game().GetPlayer(Context.User);

            if (plr != GameChannel.Game().CurrentPlayer())
                return;

            if (!plr.AlreadyDrawn())
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                .WithDescription($"⏩ You need to draw a card before you can skip {Context.User.Mention}")).ConfigureAwait(false);
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                .WithDescription($"⏩ {Context.User.Mention} skipped their turn")).ConfigureAwait(false);

            int next_current_player_index = GameChannel.Game().CurrentPlayerIndex();
            UnoPlayer new_current_player = null;
            if (!GameChannel.Game().IsGemeReversed())
                new_current_player = GameChannel.Game().SkipToPlayer(
                GameChannel.Game().CurrentPlayerIndex(), 0, true, ref next_current_player_index);
            else
                new_current_player = GameChannel.Game().SkipToPlayer(
                GameChannel.Game().CurrentPlayerIndex(), 0, false, ref next_current_player_index);

            GameChannel.Game().SetCurrentPlayer(new_current_player);
            GameChannel.Game().SetCurrentPlayerIndex(next_current_player_index);
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                                .WithDescription($"👉 It is your turn {new_current_player.User().Mention}")).ConfigureAwait(false);

            GameChannel.Game().Update();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Draw()
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);
            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning()) return;

            if (!GameChannel.Game().PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} You must be playing Uno to draw a card"))
                    .ConfigureAwait(false);
                return;
            }
            var display_msg = new StringBuilder();
            var plr = GameChannel.Game().GetPlayer(Context.User);

            if (!plr.AlreadyDrawn())
            {
                plr.Draw();
                GameChannel.Game().Update();
                display_msg.AppendLine($"{Context.User.Mention} drew a card");
            }
            else
                display_msg.AppendLine($"{Context.User.Mention} You already drew this turn");
            
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Hand()
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);
            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning()) return;

            var display_msg = new StringBuilder();
            if (!GameChannel.Game().PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} You don't have cards because you are not playing Uno!")).ConfigureAwait(false);
                return;
            }

            var plr = GameChannel.Game().GetPlayer(Context.User);
            await Context.User.SendConfirmAsync("**---- Your Hand: UNO Cards ----**").ConfigureAwait(false);
            plr.ShowCards();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Leave()
        {//!< TODO
            UnoChannel GameChannel = UnoChannel.GetGameChannel(Context.Channel);
            if ((GameChannel == null) || !GameChannel.Game().IsGameRunning()) return;

            if (!GameChannel.Game().PlayerExists(Context.User)) return;

            UnoPlayer plr = GameChannel.Game().GetPlayer(Context.User);
            plr.Leave();

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                .WithDescription($"{Context.User.Mention} left the game")).ConfigureAwait(false);

            GameChannel.Game().Update();
        }
    }
}