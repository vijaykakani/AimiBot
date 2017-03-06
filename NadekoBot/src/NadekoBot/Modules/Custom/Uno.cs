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
    public class UnoConfig
    {
        //  0 (false) = finish when first player is declared the winner
        //  1 (true)  = continue until second last player finishes
        public bool GameMode { get; set; } = false;

        public int MinimumPlayers { get; set; } = 2;
        public int MaximumPlayers { get; set; } = 6;

        public int MinimumBetAmount { get; set; } = 20;
        public int MaximumBetAmount { get; set; } = 500;
        public int BetMultiplier { get; set; } = 2;
        public float BetPercentageWinner { get; set; } = 0.7f;
        public float BetPercentageLoser { get; set; } = 0.3f;

        public int CardsPerPlayer { get; set; } = 7;
    }

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

        public bool IsActionCard()
        {
            if (_isPlus2 || _isSkip || _isReverse)
                return true;
            return false;
        }

        public bool IsStarCard()
        {
            if (_isWild || _isPlus4)
                return true;
            return false;
        }

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

        public async void SendInfo(IUser usr, bool sendImage = false)
        {
            var str = new StringBuilder();
            str.AppendLine("`ID:` " + _cardID);
            str.AppendLine("`Label:` " + UnoChannel.FirstCharToUpper(_label) + " Card");
            str.AppendLine("`Color:` " + UnoChannel.FirstCharToUpper(_color));
            if (sendImage)
            {
                await usr.SendFileAsync(
                                File.Open(_imagePath, FileMode.OpenOrCreate),
                                new FileInfo(_imagePath).Name, str.ToString())
                                    .ConfigureAwait(false);
            }
            else
            {
                await usr.SendMessageAsync(str.ToString()).ConfigureAwait(false);
            }
        }

        public string GetInfo()
        {
            var str = new StringBuilder();
            str.AppendLine("`ID:` " + _cardID);
            str.AppendLine("`Label:` " + UnoChannel.FirstCharToUpper(_label) + " Card");
            str.AppendLine("`Color:` " + UnoChannel.FirstCharToUpper(_color));
            return str.ToString();
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

        private int _bet = -1;

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

        public int Bet() { return _bet; }
        public void SetBet(int val) { _bet = val; }

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

        public async void ShowCards()
        {
            var str = new StringBuilder();
            foreach (var card in _cards)
                str.AppendLine(card.GetInfo());

            await _user.SendMessageAsync(str.ToString());
        }

        public async void Draw(int times = 1)
        {
            _game.UpdateDeck();

            var t = 0;
            var str = new StringBuilder();

            foreach (var card in _game.Deck())
            {
                if (card.CanUse()/* && ((card.Color()== "red") || card.IsStarCard())*/)
                {
                    t++;
                    card.SetID(_cards.Count() + 1);
                    card.SetCardTaken(true);
                    str.AppendLine(card.GetInfo());
                    _cards.Add(card);

                    if (t == times) break;
                }
            }

            _alreadyDrawn = true;
            _firstUnoCalled = false;

            await _user.SendMessageAsync(str.ToString());
        }

        public void RemoveCard(UnoCard card)
        {
            card.SetID(0);
            card.SetCardTaken(false);
            _cards.Remove(card);
        }

        public void RemoveCards()
        {
            var s_cards = new List<UnoCard>(_cards);
            foreach (var card in s_cards)
                RemoveCard(card);
        }

        public void Leave()
        {
            RemoveCards();
            if (_finishedUno) _game.DecreasePlayersFinishedOrder();

            _game.Players().Remove(this);

            foreach (var plr in _game.Players())
                if (plr.FinishedUnoOrder() > _finishedUnoOrder)
                    plr.SetFinishedUnoOrder(plr.FinishedUnoOrder() - 1);
        }
    }

    public class UnoGame
    {
        private List<UnoPlayer> _players = new List<UnoPlayer>();
        private List<UnoCard> _deck = new List<UnoCard>();
        private UnoCard _lastPlacedCard;
        private IMessageChannel _channel;
        private UnoChannel _gameChannel;
        private UnoConfig _config = new UnoConfig();

        private UnoPlayer _currentPlayer;
        private int _currentPlayerIndex = -1;
        private int _startingPlayerIndex = -1;
        private int _playersFinishedOrder = -1;

        private bool _isGameRunning = false;
        private bool _isGameReversed = false;
        private bool _isWinnerDeclared = false;
        private bool _isBetActive = false;

        public List<UnoPlayer> Players() { return _players; }
        public List<UnoCard> Deck() { return _deck; }
        public void SetDeck(List<UnoCard> cards) { _deck.Clear(); _deck = cards; }

        public UnoCard LastPlacedCard() { return _lastPlacedCard; }
        public void SetLastPlacedCard(UnoCard card) { _lastPlacedCard = card; }

        public IMessageChannel Channel() { return _channel; }
        public void SetChannel(IMessageChannel ch) { _channel = ch; }
        public UnoChannel GameChannel() { return _gameChannel; }
        public void SetGameChannel(UnoChannel gameChannel) { _gameChannel = gameChannel; }
        public UnoConfig Config() { return _config; }
        public void SetConfig(UnoConfig config) { _config = config; }

        public bool IsGameRunning() { return _isGameRunning; }
        public void SetGameRunning(bool val) { _isGameRunning = val; }

        public bool IsGemeReversed() { return _isGameReversed; }
        public void SetGameReversed(bool val) { _isGameReversed = val; }
        
        public bool IsWinnerDeclared() { return _isWinnerDeclared; }
        public void SetWinnerDeclared(bool val) { _isWinnerDeclared = val; }

        public bool IsBetActive() { return _isBetActive; }
        public void SetBetActive(bool val) { _isBetActive = val; }

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
        
        public void IncreasePlayersFinishedOrder()
        {
            if (_playersFinishedOrder == -1)
                _playersFinishedOrder = 0;
            _playersFinishedOrder += 1;
        }
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
            if (_deck.Count() > 0)
            {
                foreach (var card in _deck)
                    card.Reset();
                return;
            }

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

                //  Wild or Plus 4
                if (parts_of_name.Count() == 2)
                {
                    type = parts_of_name[1];
                    label = labels[type];
                    multiply = 4;
                }
                //  number, skip, reverse, plus 2
                else
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
            _players = new List<UnoPlayer>();

            LoadCards();
            ShuffleDeck();
        }

        public async void Update()
        {
            //  do not process further until the game is running
            if (!_isGameRunning) return;

            if (NoPlayers() || (_players.Count() < _config.MinimumPlayers))
            {
                await _channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"There are less than players required to play. The game is shutting down.")).ConfigureAwait(false);
                Reset();
            }
            else
            {
                //  check whether uno is complete
                if (_config.GameMode && (_playersFinishedOrder + 1) == _players.Count())
                {
                    var display_msg = new StringBuilder();
                    display_msg.AppendLine("UNO finished! Results:\n");

                    var c_val = _playersFinishedOrder + 1;
                    foreach (var plr in _players)
                        if (!plr.FinishedUno())
                            plr.SetFinishedUnoOrder(c_val++);

                    var uno_players = _players.OrderBy(p => p.FinishedUnoOrder()).ToList();

                    foreach (var plr in uno_players)
                        display_msg.AppendLine($"`{plr.FinishedUnoOrder()}{UnoChannel.GetDigitSuffix(plr.FinishedUnoOrder())}:` {plr.User()}");

                    await _channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription(display_msg.ToString())).ConfigureAwait(false);

                    Reset();
                    return;
                }
                else if (!_config.GameMode && _isWinnerDeclared)
                {
                    await _channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                        .WithDescription("With the winner declared, the game has shutdown.")).ConfigureAwait(false);
                    Reset();
                    return;
                }

                //  ensure that all players have bet
                //  and if one or many players did not bet
                //  deactivate the betting system
                foreach (var plr in _players)
                    if (plr.Bet() == 0)
                        _isBetActive = false;

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

            UpdateDeck();

            foreach (var card in _deck)
            {
                if (card.CanUse()/* && ((card.Color() == "red") || card.IsStarCard())*/)
                {
                    id++;
                    card.SetID(id);
                    card.SetCardTaken(true);
                    cards.Add(card);
                }

                if (id == _config.CardsPerPlayer) break;
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
                            card.Reset();

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

            foreach (var card in _deck)
                card.Reset();

            _lastPlacedCard = null;
            _currentPlayer = null;

            _currentPlayerIndex = -1;
            _startingPlayerIndex = -1;
            _playersFinishedOrder = -1;

            _isGameRunning = false;
            _isGameReversed = false;
            _isWinnerDeclared = false;
            _isBetActive = false;
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

        public static bool DoesGameChannelExist(IMessageChannel ch)
        {
            foreach (var chan in _channels)
            {
                if (chan._channel == ch)
                    return true;
            }
            return false;
        }

        public static UnoChannel GetGameChannel(IMessageChannel ch)
        {
            foreach (var chan in _channels)
            {
                if (chan._channel == ch)
                    return chan;
            }
            return null;
        }

        public static UnoGame GetGame(IMessageChannel ch)
        {
            foreach (var chan in _channels)
            {
                if (chan._channel == ch)
                    return chan._game;
            }
            return null;
        }

        public static string GetDigitSuffix(int val)
        {
            switch (val)
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

        public static string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        public static bool IsGameActive(IMessageChannel Channel)
        {
            var Game = GetGame(Channel);
            if (!DoesGameChannelExist(Channel) || !Game.IsGameRunning())
                return false;
            return true;
        }

        public UnoChannel(IMessageChannel ch)
        {
            _channel = ch;

            _game = new UnoGame();
            _game.SetChannel(ch);
            _game.SetGameChannel(this);

            _channels.Add(this);
        }
    }

    [NadekoModule("Uno", "u!")]
    public partial class Uno : DiscordModule
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Start()
        {
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
                    display_msg.AppendLine($"The game is already running.");
                else
                {
                    try
                    {
                        GameChannel.Game().Start();
                    }
                    catch (Exception ex)
                    {
                        error_message.AppendLine($"Message: {ex.Message}\nSource: Starting game\n");
                    }

                    try
                    {
                        UnoCard placementCard = GameChannel.Game().GetRandomCard();
                        while (placementCard.IsActionCard() || placementCard.IsStarCard()/* || (placementCard.Color() != "red")*/)
                            placementCard = GameChannel.Game().GetRandomCard();
                        placementCard.SetCardPlaced(true);

                        await Context.Channel.SendFileAsync(
                                        File.Open(placementCard.ImagePath(), FileMode.OpenOrCreate),
                                        new FileInfo(placementCard.ImagePath()).Name, "First Card Placed:")
                                            .ConfigureAwait(false);

                        GameChannel.Game().SetLastPlacedCard(placementCard);
                    }
                    catch (Exception ex)
                    {
                        error_message.AppendLine($"Message: {ex.Message}\nSource: Placing the first card\n");
                    }

                    var user = Context.User as IGuildUser;
                    display_msg.AppendLine($"@everyone\n{user.Nickname} started Uno!\n");
                    display_msg.AppendLine("Game paused.");
                    display_msg.Append($"Waiting for a minimum of `{GameChannel.Game().Config().MinimumPlayers} players` to join so the game can be played.");
                }

                display_msg.AppendLine("To join the game, use `u!join` and if you want to bet, use `u!join BetAmount`");
                display_msg.AppendLine("To see the rules of the game, use `u!rules`");
                display_msg.AppendLine("To see the list of all Uno Commands, use `u!cmds`");
            }
            catch (Exception ex)
            {
                error_message.AppendLine($"Message: {ex.Message}\nSource: Running the start\n");
            }
            
            if (!string.IsNullOrWhiteSpace(display_msg.ToString()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithOkColor().WithDescription(display_msg.ToString())).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error_message.ToString().Trim()))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithErrorColor().WithTitle("Error").WithDescription(error_message.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stop()
        {
            var Game = UnoChannel.GetGame(Context.Channel);
            if (!UnoChannel.DoesGameChannelExist(Context.Channel) || !Game.IsGameRunning())
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("The game is not running. Type `u!start` to run it")).ConfigureAwait(false);
            else
            {
                if (!Game.PlayerExists(Context.User))
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Only players are allowed to stop the game")).ConfigureAwait(false);
                    return;
                }

                Game.Reset();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithTitle("Uno").WithDescription("Game has stopped")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Shuffle()
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot shuffle deck when the game isn't running")).ConfigureAwait(false);
            else
            {
                var Game = UnoChannel.GetGame(Context.Channel);

                if (!Game.PlayerExists(Context.User))
                {
                    await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Only players are allowed to shuffle the deck")).ConfigureAwait(false);
                    return;
                }

                Game.ShuffleDeck();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription("The deck is shuffled")).ConfigureAwait(false);
                Game.UpdateDeck();
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task NextPlayer()
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot see next player because the game is inactive")).ConfigureAwait(false);
            else
            {
                var Game = UnoChannel.GetGame(Context.Channel);
                if (Game.NoPlayers()) return;

                UnoPlayer plr = Game.GetNextPlayer();
                var u_plr = plr.User() as IGuildUser;
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription($"The next player is {u_plr.Nickname}")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task CurrentPlayer()
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot see the current player because the game is inactive")).ConfigureAwait(false);
            else
            {
                var Game = UnoChannel.GetGame(Context.Channel);
                if (Game.NoPlayers()) return;
                
                var mention = "";
                if (Game.CurrentPlayer().User() == Context.User)
                    mention = "you";
                else
                    mention = Game.CurrentPlayer().User().Mention;

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription($"The current player is {mention}")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task PreviousPlayer()
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot see next player because the game is inactive")).ConfigureAwait(false);
            else
            {
                var Game = UnoChannel.GetGame(Context.Channel);
                if (Game.NoPlayers()) return;

                UnoPlayer plr = Game.GetPreviousPlayer();
                var mention = "";
                if (plr.User() == Context.User)
                    mention = "you";
                else
                    mention = plr.User().Mention;

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithDescription($"The previous player is {mention}")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task CurrentCard()
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription("Cannot see next player because the game is inactive")).ConfigureAwait(false);
            else
            {
                var Game = UnoChannel.GetGame(Context.Channel);

                await Context.Channel.SendFileAsync(
                                        File.Open(Game.LastPlacedCard().ImagePath(), FileMode.OpenOrCreate),
                                        new FileInfo(Game.LastPlacedCard().ImagePath()).Name, "Last placed card:")
                                        .ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Join(int bet = 0)
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription("Cannot join when the game is not running. To run the game, use: `u!start`")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            var display_msg = new StringBuilder();

            if (Game.Players().Count() == Game.Config().MaximumPlayers)
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                .WithDescription($"Cannot join because the maximum amount of {Game.Config().MaximumPlayers} players have already joined"))
                .ConfigureAwait(false);
                return;
            }

            if (Game.PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"{Context.User.Mention} You are already playing")).ConfigureAwait(false);
                return;
            }

            Game.HandCardsOut(Context.User);
            display_msg.AppendLine($"{Context.User.Mention} joined the game\n");

            display_msg.AppendLine("To check the cards in your hand, use `u!hand`");
            display_msg.AppendLine("To see the rules of the game, use `u!rules`");
            display_msg.AppendLine("To see the list of all Uno Commands, use `u!cmds`");

            UnoPlayer plr = Game.GetPlayer(Context.User);

            //  set the starting player
            if ((Game.CurrentPlayerIndex() == -1) && (Game.GetStartingPlayerIndex() == -1))
            {
                Game.SetCurrentPlayer(plr);
                Game.SetCurrentPlayerIndex(0);
                Game.SetStartingPlayerIndex(0);

                display_msg.AppendLine($"\n{Context.User.Mention} is the starting player");
            }

            if (bet > 0)
            {
                if (bet < Game.Config().MinimumBetAmount)
                {
                    await Context.Channel.SendErrorAsync($"You can't bet less than {Game.Config().MinimumBetAmount}{NadekoBot.BotConfig.CurrencySign}")
                                    .ConfigureAwait(false);
                    return;
                }

                if (bet > Game.Config().MaximumBetAmount)
                {
                    await Context.Channel.SendErrorAsync($"You can't bet more than {Game.Config().MaximumBetAmount}{NadekoBot.BotConfig.CurrencySign}")
                                    .ConfigureAwait(false);
                    return;
                }

                long userFlowers;
                using (var uow = DbHandler.UnitOfWork())
                {
                    userFlowers = uow.Currency.GetOrCreate(Context.User.Id).Amount;
                }

                if (userFlowers < bet)
                {
                    await Context.Channel
                        .SendErrorAsync($"{Context.User.Mention} You don't have enough {NadekoBot.BotConfig.CurrencyPluralName}. You only have {userFlowers}{NadekoBot.BotConfig.CurrencySign}").ConfigureAwait(false);
                    return;
                }

                plr.SetBet(bet);
                Game.SetBetActive(true);
            }

            if (Game.Players().Count() < Game.Config().MinimumPlayers)
            {
                var diff = Game.Config().MinimumPlayers - Game.Players().Count();
                display_msg.AppendLine($"\nWaiting for `{diff}` more players".ToString().SnPl(diff) + " to join before the game can start.");
            }
            else if ((Game.Players().Count() >= Game.Config().MinimumPlayers) && !Game.IsGameRunning())
            {
                display_msg.AppendLine($"\nLet the game begin.");
                Game.SetGameRunning(true);
            }

            Game.Update();

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithOkColor().WithDescription(display_msg.ToString())).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Place([Remainder] string param = null)
        {
            if (string.IsNullOrWhiteSpace(param))
                return;

            if (!UnoChannel.IsGameActive(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"The game is inactive.")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            if (!Game.PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"{Context.User.Mention} You have not joined yet. Do `u!join` to join the game.")).ConfigureAwait(false);
                return;
            }

            if (Game.Players().Count() < Game.Config().MinimumPlayers)
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"Less than minimum players active.")).ConfigureAwait(false);
                return;
            }

            var plr = Game.GetPlayer(Context.User);
            UnoPlayer new_current_player = null;
            int new_current_player_index = -1;

            if (plr != Game.CurrentPlayer())
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} Please wait. It isn't your turn yet.\nUse `u!currentplayer` or `u!cp` to see whos turn it is")).ConfigureAwait(false);
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
                        if (!card.IsPlus4())
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
                        if ((first_card.Label() != Game.LastPlacedCard().Label())
                            && (first_card.Color() != Game.LastPlacedCard().Color()))
                        {
                            error_msg.AppendLine($"Your first `{first_card.Label()} Number Card` does not match with the last card played");
                            proceed = false;
                        }
                    }
                }

                //  SKIP CODE CHECK
                //  Processing when there's only Skip Action Card
                if (!Number && proceed && !Wild && !Plus4 && !Plus2 && Skip && !Reverse)
                {
                    UnoCard first_card = cards[0];
                    if ((first_card.Label() != Game.LastPlacedCard().Label())
                            && (first_card.Color() != Game.LastPlacedCard().Color()))
                    {
                        error_msg.AppendLine($"Your first `Skip Card` does not match in color with the last card played");
                        proceed = false;
                    }
                }

                //  REVERSE CODE CHECK
                //  Processing when there's only Reverse Action Card
                if (!Number && proceed && !Wild && !Plus4 && !Plus2 && !Skip && Reverse)
                {
                    UnoCard first_card = cards[0];
                    if ((first_card.Label() != Game.LastPlacedCard().Label())
                            && (first_card.Color() != Game.LastPlacedCard().Color()))
                    {
                        error_msg.AppendLine($"Your first `Reverse Card` does not match with the last card played");
                        proceed = false;
                    }
                }

                //  PLUS 2 CODE CHECK
                //  Processing when there's only Reverse Action Card
                if (!Number && proceed && !Wild && !Plus4 && Plus2 && !Skip && !Reverse)
                {
                    UnoCard first_card = cards[0];
                    if ((first_card.Label() != Game.LastPlacedCard().Label())
                            && (first_card.Color() != Game.LastPlacedCard().Color()))
                    {
                        error_msg.AppendLine($"Your first `Plus 2 Card` does not match with the last card played");
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

                    new_current_player_index = Game.CurrentPlayerIndex();
                    new_current_player = Game.CurrentPlayer();

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
                        foreach (var card in cards)
                        {
                            if (card.IsSkip())
                            {
                                do
                                {
                                    if (!Game.IsGemeReversed())
                                        new_current_player = Game.SkipToPlayer(new_current_player_index, 1, true, ref new_current_player_index);
                                    else
                                        new_current_player = Game.SkipToPlayer(new_current_player_index, -1, false, ref new_current_player_index);
                                }
                                while (new_current_player.FinishedUno()) ;
                            }

                            if (card.IsReverse())
                            {
                                if (Game.IsGemeReversed())
                                    Game.SetGameReversed(false);
                                else
                                    Game.SetGameReversed(true);

                                do
                                {
                                    //  the effect goes to the person after the chosen one
                                    if (!Game.IsGemeReversed())
                                        new_current_player = Game.SkipToPlayer(new_current_player_index, 0, true, ref new_current_player_index);
                                    else
                                        new_current_player = Game.SkipToPlayer(new_current_player_index, 0, false, ref new_current_player_index);
                                }
                                while (new_current_player.FinishedUno());
                            }
                        }

                        if (Number && !Skip && !Reverse)
                        {
                            if (!Game.IsGemeReversed())
                                new_current_player = Game.SkipToPlayer(new_current_player_index, 0, true, ref new_current_player_index);
                            else
                                new_current_player = Game.SkipToPlayer(new_current_player_index, 0, false, ref new_current_player_index);
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
                            foreach (var card in cards)
                            {
                                if (Game.IsGemeReversed())
                                    Game.SetGameReversed(false);
                                else
                                    Game.SetGameReversed(true);

                                do
                                {
                                    //  the effect goes to the person after the chosen one
                                    if (!Game.IsGemeReversed())
                                        new_current_player = Game.SkipToPlayer(new_current_player_index, 0, true, ref new_current_player_index);
                                    else
                                        new_current_player = Game.SkipToPlayer(new_current_player_index, 0, false, ref new_current_player_index);
                                }
                                while (new_current_player.FinishedUno());
                            }
                        }

                        if (Number || Plus2 || Skip)
                        {
                            do
                            {
                                if (!Game.IsGemeReversed())
                                    new_current_player = Game.SkipToPlayer(
                                    Game.CurrentPlayerIndex(), go_turns, true, ref new_current_player_index);
                                else
                                    new_current_player = Game.SkipToPlayer(
                                    Game.CurrentPlayerIndex(), -go_turns, false, ref new_current_player_index);
                            }
                            while (new_current_player.FinishedUno());
                        }
                    }

                    //  Set that all the cards selected to be placed
                    foreach (var card in cards)
                    {
                        card.SetCardPlaced(true);
                        plr.RemoveCard(card);
                    }

                    var file_msg = new StringBuilder();
                    file_msg.AppendLine($"✋ {plr.User().Mention} placed {cards.Count()} cards".ToString().SnPl(cards.Count()) + $" of » {last_card.Label().ToUpperInvariant()} «");

                    for (var i = 0; i < cards.Count(); i++)
                    {
                        if ((i + 1) == cards.Count())
                            file_msg.AppendLine($":arrow_forward: {UnoChannel.FirstCharToUpper(cards[i].Label())} ({UnoChannel.FirstCharToUpper(cards[i].Color())})");
                        else
                            file_msg.AppendLine($":arrow_forward: {UnoChannel.FirstCharToUpper(cards[i].Label())} ({UnoChannel.FirstCharToUpper(cards[i].Color())}) 🔽");
                    }

                    await Context.Channel.SendFileAsync(
                                        File.Open(last_card.ImagePath(), FileMode.OpenOrCreate),
                                        new FileInfo(last_card.ImagePath()).Name, file_msg.ToString())
                                        .ConfigureAwait(false);

                    //  update the cards with the player by giving proper ids
                    plr.UpdateCards();

                    //  showing the names of all the players skipped using only the SKIP cards
                    if (!Number && !Wild && !Plus4 && !Plus2 && Skip && !Reverse)
                    {
                        display_msg.AppendLine($"➤ The game has skipped {no_skip} turns".ToString().SnPl(no_skip));

                        for (var i = 0; i < no_skip; i++)
                        {
                            int next_player_index = 0;
                            UnoPlayer next_player = null;

                            if (!Game.IsGemeReversed())
                                next_player = Game.SkipToPlayer(Game.CurrentPlayerIndex(), i, true, ref next_player_index);
                            else
                                next_player = Game.SkipToPlayer(Game.CurrentPlayerIndex(), -i, false, ref next_player_index);
                            display_msg.AppendLine($"⏩ {next_player.User().Mention} got skipped");
                        }
                    }

                    //  sets the starting index for the new round
                    if (Reverse)
                    {
                        Game.SetStartingPlayerIndex(new_current_player_index);
                        display_msg.AppendLine($"➤ {plr.User().Mention} reversed {no_reverse} times".ToString().SnPl(no_reverse));
                    }

                    if (DrawCards > 0)
                    {
                        await new_current_player.User()
                            .SendConfirmAsync($"**---- You Drew {DrawCards} UNO Cards".ToString().SnPl(DrawCards) + " ----**").ConfigureAwait(false);

                        display_msg.AppendLine($":scream: {plr.User().Mention} made {new_current_player.User().Mention} draw {DrawCards} cards");

                        new_current_player.Draw(DrawCards);
                    }

                    //  sets the new current player
                    Game.SetCurrentPlayer(new_current_player);
                    Game.SetCurrentPlayerIndex(new_current_player_index);
                    Game.SetLastPlacedCard(last_card);

                    //  process the UNO completion
                    if (plr.FirstUnoCalled() || plr.Cards().Count() == 0)
                    {
                        plr.SetFinishedUno(true);

                        if (Game.Config().GameMode)
                        {
                            Game.IncreasePlayersFinishedOrder();
                            plr.SetFinishedUnoOrder(Game.PlayersFinishedOrder());
                            display_msg.AppendLine($"🎇 {plr.User().Mention} finished in {plr.FinishedUnoOrder()}{UnoChannel.GetDigitSuffix(plr.FinishedUnoOrder())} place. Well done!");
                        }
                        else
                        {
                            Game.SetWinnerDeclared(true);
                            display_msg.AppendLine($"🎇 {plr.User().Mention} finished 1st! Well done!");
                        }
                    }
                    else if (plr.Cards().Count() == 1)
                    {
                        plr.SetFirstUnoCalled(true);
                        display_msg.AppendLine($"⛳ {plr.User().Mention} called UNO! One card remaining!");
                    }
                    
                    //  Checks for bets and processes them when the player finish order reaches 1
                    if (Game.IsBetActive() && 
                        ((Game.Config().GameMode && (Game.PlayersFinishedOrder() == 1)) || (!Game.Config().GameMode && Game.IsWinnerDeclared()))
                        )
                    {
                        //  for only two players, the winner takes all
                        //  combination of the bet made by both players and the first player takes it
                        

                        //  combining bets of all players
                        var total_bet_amount = 0;
                        foreach (var s_plr in Game.Players())
                        {
                            total_bet_amount += s_plr.Bet();
                            await CurrencyHandler.RemoveCurrencyAsync(s_plr.User(), "Uno Bet", s_plr.Bet(), false).ConfigureAwait(false);
                        }

                        //  divide them into winner and loser awards
                        var winner_award = (int)Math.Round(total_bet_amount * Game.Config().BetPercentageWinner);
                        var loser_award = (int)Math.Round((total_bet_amount * Game.Config().BetPercentageLoser) / (Game.Players().Count() - 1));

                        var s_plr_user = plr as IGuildUser;
                        
                        //  award the winner
                        await CurrencyHandler.AddCurrencyAsync(plr.User(), "Winning Uno", winner_award, false).ConfigureAwait(false);
                        display_msg.AppendLine($"{s_plr_user.Nickname} received {winner_award} {NadekoBot.BotConfig.CurrencySign}");

                        foreach (var s_plr in Game.Players())
                        {
                            if (s_plr == plr) return;

                            var s_user = s_plr as IGuildUser;
                            await CurrencyHandler.AddCurrencyAsync(plr.User(), "Playing Uno", loser_award, false).ConfigureAwait(false);
                            display_msg.AppendLine($"{s_user.Nickname} received {loser_award} {NadekoBot.BotConfig.CurrencySign}");
                        }

                        /*  Old code for betting for only the first to win while others lose
                        foreach (var s_plr in Game.Players())
                        {
                            if (s_plr.Bet() > 0)
                            {
                                await CurrencyHandler.RemoveCurrencyAsync(s_plr.User(), "Uno Bet", s_plr.Bet(), false).ConfigureAwait(false);

                                if (s_plr == plr)
                                {
                                    var award = s_plr.Bet() * Game.Config().BetMultiplier;
                                    display_msg.AppendLine($"{s_plr.User().Mention} has won their bet! They received {award}{NadekoBot.BotConfig.CurrencySign}");
                                    await CurrencyHandler.AddCurrencyAsync(s_plr.User(), "Uno Bet", award, false).ConfigureAwait(false);
                                }
                                else
                                {
                                    await s_plr.User().SendMessageAsync($":frowning: You lost the bet. Better luck next time.").ConfigureAwait(false);
                                }
                            }
                        }
                        */
                    }

                    success = true;
                }
            }

            if (success)
            {
                if ((Game.Config().GameMode && (Game.PlayersFinishedOrder() + 1) < Game.Players().Count()) || (!Game.Config().GameMode && !Game.IsWinnerDeclared()))
                    display_msg.AppendLine($"\n👉 It is your turn {new_current_player.User().Mention} 👈");

                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithOkColor().WithDescription(display_msg.ToString())).ConfigureAwait(false);
                    
                Game.Update();
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
        {
            if (!UnoChannel.IsGameActive(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"The game is inactive.")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            if (!Game.PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} You need to join the game first.")).ConfigureAwait(false);
                return;
            }

            if (Game.Players().Count() < Game.Config().MinimumPlayers)
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"Less than minimum players active.")).ConfigureAwait(false);
                return;
            }

            UnoPlayer plr = Game.GetPlayer(Context.User);

            if (plr != Game.CurrentPlayer())
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"{Context.User.Mention} Wait for your turn to skip.")).ConfigureAwait(false);
                return;
            }

            //  Checks whether the player has either:
            //
            //  - drawn a card
            //  
            //  to be able to skip so the game moves onto the next player
            if (!plr.AlreadyDrawn())
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                .WithDescription($"⏩ {Context.User.Mention} You need to draw a card before you can skip {Context.User.Mention}")).ConfigureAwait(false);
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                .WithDescription($"⏩ {Context.User.Mention} skipped their turn")).ConfigureAwait(false);

            int next_current_player_index = Game.CurrentPlayerIndex();
            UnoPlayer new_current_player = Game.CurrentPlayer();
            do
            {
                if (!Game.IsGemeReversed())
                    new_current_player = Game.SkipToPlayer(next_current_player_index, 0, true, ref next_current_player_index);
                else
                    new_current_player = Game.SkipToPlayer(next_current_player_index, 0, false, ref next_current_player_index);
            }
            while (new_current_player.FinishedUno());

            Game.SetCurrentPlayer(new_current_player);
            Game.SetCurrentPlayerIndex(next_current_player_index);
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                                .WithDescription($"👉 It's your turn {new_current_player.User().Mention}")).ConfigureAwait(false);

            Game.Update();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Draw()
        {
            if (!UnoChannel.IsGameActive(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"The game is inactive.")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            if (!Game.PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} You must play Uno to draw a card"))
                    .ConfigureAwait(false);
                return;
            }

            if (Game.Players().Count() < Game.Config().MinimumPlayers)
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"Less than minimum players active.")).ConfigureAwait(false);
                return;
            }

            var display_msg = new StringBuilder();
            var plr = Game.GetPlayer(Context.User);

            if (plr != Game.CurrentPlayer())
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"{Context.User.Mention} Wait for your turn to draw.")).ConfigureAwait(false);
                return;
            }

            if (!plr.AlreadyDrawn())
            {
                await plr.User().SendConfirmAsync("**---- You Drew This UNO Card ----**").ConfigureAwait(false);
                plr.Draw();
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithOkColor()
                    .WithDescription($"{Context.User.Mention} drew a card")).ConfigureAwait(false);
            }
            else
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"{Context.User.Mention} You already drew this turn")).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Hand()
        {
            if (!UnoChannel.DoesGameChannelExist(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"The game is inactive.")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            if (!Game.PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithErrorColor()
                    .WithDescription($"{Context.User.Mention} You must join the game in order to have cards!")).ConfigureAwait(false);
                return;
            }

            var plr = Game.GetPlayer(Context.User);
            await Context.User.SendConfirmAsync("**---- Your Hand: UNO Cards ----**").ConfigureAwait(false);
            plr.ShowCards();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Leave()
        {
            if (!UnoChannel.IsGameActive(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"The game is inactive.")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            if (!Game.PlayerExists(Context.User))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"{Context.User.Mention} You're not part of the game to leave.")).ConfigureAwait(false);
                return;
            }

            UnoPlayer plr = Game.GetPlayer(Context.User);
            plr.Leave();

            //  remove the leaving player's bet
            if (plr.Bet() > 0)
                await CurrencyHandler.RemoveCurrencyAsync(plr.User(), "Uno Bet", plr.Bet(), false).ConfigureAwait(false);

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno").WithOkColor()
                .WithDescription($"{Context.User.Mention} left the game")).ConfigureAwait(false);

            Game.Update();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Cards()
        {
            if (!UnoChannel.IsGameActive(Context.Channel))
            {
                await Context.Channel.EmbedAsync(new EmbedBuilder().WithTitle("Uno")
                    .WithDescription($"The game is inactive.")).ConfigureAwait(false);
                return;
            }

            var Game = UnoChannel.GetGame(Context.Channel);
            var str = new StringBuilder();
            
            str.AppendLine($"Deck  : {Game.Deck().Count()} cards\n");

            var count_placed = 0;
            var count_taken = 0;
            var count_remain = 0;
            foreach (var card in Game.Deck())
            {
                if (card.IsCardPlaced())
                    count_placed++;

                if (card.IsCardTaken())
                    count_taken++;

                if (card.CanUse())
                    count_remain++;
            }

            str.AppendLine($"Taken : {count_taken}");
            str.AppendLine($"Placed: {count_placed}");
            str.AppendLine($"Remain: {count_remain}");

            await Context.Channel.SendConfirmAsync($"**Uno**\n*List of cards in deck*\n```{str.ToString()}\n```").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Cmds(string param = null)
        {
            var str = new StringBuilder();
            str.AppendLine("**__List of Commands__**\n");
            str.AppendLine("All commands have a prefix of `u!` (for example: u!start)\n");
            str.AppendLine("`start` — Starts the game");
            str.AppendLine("`stop` — Stops the game");
            str.AppendLine("`join` — Joins the game\n`u!join <bet amount>` — Bets the amount that multiplies for finishing 1st");
            str.AppendLine("`hand` — Shows the cards in your hand");
            str.AppendLine("`place CardID` — Places the cards by their ids");
            str.AppendLine("`shuffle` — Shuffles the deck");
            str.AppendLine("`cards` — Shows the cards in deck, taken, placed and remaining");
            str.AppendLine("`currentcard` or `cc` — Shows the last card placed");
            str.AppendLine("`draw` — Draw a card from the deck");
            str.AppendLine("`skip` — Skips your turn");
            str.AppendLine("`nextplayer` or `np` — Shows the next player");
            str.AppendLine("`currentplayer` or `cp` — Shows the current player");
            str.AppendLine("`previousplayer` or `pp` — Shows the previous player");
            str.AppendLine("`leave` — Leaves the game");
            str.AppendLine("`rules` — See the list of rules");
            await Context.User.SendConfirmAsync(str.ToString());
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Rules(string param = null)
        {
            var str = new StringBuilder();
            str.AppendLine("**Uno Rules**\n");

            if (string.IsNullOrWhiteSpace(param))
            {
                str.AppendLine("You can use `u!cmds` to see the list of Uno Commands.\n");

                str.AppendLine($"A minimum of 2 players must join in order to play the game.");
                str.AppendLine($"A maximum of 6 players are allowed to play the game.");
                str.AppendLine($"Each player gets 7 cards from a deck of 108 cards.");
                str.AppendLine($"Players can view the cards in their hands using `u!hand`");
                str.AppendLine($"Players can place selected cards using the ids: u!place CardID1 CardID2 ...");
                str.AppendLine($"Players can only draw 1 card per turn.");
                str.AppendLine($"If the player is unable to place a matching card, they must use `u!draw` and `u!skip` to pass the turn onto the next player.");

                str.AppendLine("For specific rules for a card, do `u!rules CardName` (for example: u!rules p4)");
                str.AppendLine("Players placing a card must match its label or color with the last card placed unless it is a Wild/Plus 4 Card\n");

                str.AppendLine("CardName can be in the following below:");
                str.AppendLine("```");
                str.AppendLine("n  — Number Card");
                str.AppendLine("r  — Reverse Card");
                str.AppendLine("s  — Skip Card");
                str.AppendLine("w  — Wild Card");
                str.AppendLine("p2 — Plus 2 Card");
                str.AppendLine("p4 — Plus 4 Card");
                str.AppendLine("```");
            }
            else
            {
                switch (param.Trim().ToLowerInvariant())
                {
                    case "n":
                        str.AppendLine("*Explaining Number Cards*\n");
                        str.AppendLine("You can place any number of \"Number\" cards of same or different colors with matching labels.\n");
                        str.AppendLine("__Example 1:__ You have `9 Blue` and `9 Blue`\n`9 Blue` > `9 Blue` because they have matching labels\n");
                        str.AppendLine("__Example 2:__ You have `9 Blue` and `9 Red`\n`9 Blue` > `9 Red` because the Blue and Red cards match in labels\n");
                        str.AppendLine("__Example 3:__ You have `9 Blue`, `8 Blue`\nYou can only choose one of the cards to place. Choose wisely.\n");
                        str.AppendLine("__Example 4:__ If the last card placed is a `9 Blue`, the next player has to place cards that matche that color or label\n");
                        break;

                    case "r":
                        str.AppendLine("*Explaining Reverse Cards*\n");
                        str.AppendLine("You can place any number of \"Reverse\" cards of same or different colors with matching labels.");
                        str.AppendLine("Remember that placing two set of \"reverse\" cards returns the turn back to you again.\n");
                        str.AppendLine("__Example 1:__ You have `Reverse Blue` and `Reverse Blue`\n`Reverse Blue` > `Reverse Blue` because they have matching labels and colors\n");
                        str.AppendLine("__Example 2:__ You have `Reverse Blue` and `Reverse Red`\n`Reverse Blue` > `Reverse Red` because they have matching labels\n");
                        str.AppendLine("__Example 3:__ If you place `Reverse Blue` and `Reverse Red`\nThe flow of the turns return to normal and it becomes the next players' turn\n");
                        str.AppendLine("__Example 4:__ If the last card placed is a `Reverse Blue`, the next player has to place cards that matche that color or label\n");
                        break;

                    case "s":
                        str.AppendLine("*Explaining Skip Cards*\n");
                        str.AppendLine("You can place any number of \"Skip\" cards of same or different colors with matching labels.");
                        str.AppendLine("Each skip card placed counts as a turn skipped.\n");
                        str.AppendLine("__Example 1:__ Placing one `Skip Card` means that only one turn is skipped\n");
                        str.AppendLine("__Example 2:__ Placing two `Skip Cards` means that two turns got skipped\n");
                        str.AppendLine("__Example 3:__ Placing four `Skip Cards` means that four turns got skipped\n");
                        str.AppendLine("__Example 4:__ If the last card placed is a `Skip Blue`, the next player has to place cards that matche that color or label\n");
                        break;

                    case "p2":
                        str.AppendLine("*Explaining Plus 2 Cards*\n");
                        str.AppendLine("You can place any number of \"Plus 2\" cards of same or different colors with matching labels.");
                        str.AppendLine("Each plus 2 card placed makes the target player draw 2 more cards.\n");
                        str.AppendLine("__Example 1:__ Placing one `Plus 2 Card` means that the target player only draws 2 cards\n");
                        str.AppendLine("__Example 2:__ Placing two `Plus 2 Cards` means that the target player draws 4 cards\n");
                        str.AppendLine("__Example 3:__ Placing four `Plus 2 Cards` means that the target player draws 8 cards\n");
                        str.AppendLine("__Example 4:__ If the last card placed is a `Plus 2 Blue`, the next player has to place cards that matche that color or label\n");
                        break;

                    case "w":
                        str.AppendLine("*Explaining Wild Cards*\n");
                        str.AppendLine("You can place only one of the \"Wild\" card per turn.");
                        str.AppendLine("It doesn't matter what the last placed card is when it's a wild card that's going to be placed next.");
                        str.AppendLine("You are allowed to place any number of `Skip`, `Reverse` and `Plus 2` cards.");
                        str.AppendLine("The flow of the game gets affected by how the `Skip` and `Reverse`cards are positioned upon placement.");
                        str.AppendLine("A Wild card must end with one or multiple Numer Cards of the same label with same or different colors.\n");
                        str.AppendLine("__Example 1:__ Placing `Wild` > `Skip` > `9 Blue`\nOne turn will be skipped and the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 2:__ Placing `Wild` > `Reverse` > `9 Blue`\nChanges the direction of the flow of the game and the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 3:__ Placing `Wild` > `Plus 2` > `9 Blue`\nThe next player draws 2 cards and the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 4:__ Placing `Wild` > `Plus 2` > `Skip` > `9 Blue`\nSkips one turn and makes the target player draw 2 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 5:__ Placing `Wild` > `Plus 2` > `Reverse` > `9 Blue`\nReverse the game direction and makes the previous player draw 2 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 6:__ Placing `Wild` > `Plus 2` > `Skip` > `9 Blue`\nReverse the game direction and makes the previous player draw 2 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 7:__ Placing `Wild` > `Plus 2` > `Skip` > `Reverse` > `9 Blue`\nSkips a turn and reverses the direction, which brings it back to you and forces you to draw 2 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 8:__ Placing `Wild` > `Plus 2` > `Reverse` > `Skip` > `9 Blue`\nReverses the direction and skips the previous player's turn, making the targeted player draw 2 cards with the last card placed becomes 9 Blue.\n");
                        break;

                    case "p4":
                        str.AppendLine("*Explaining Plus 4 Cards*\n");
                        str.AppendLine("You can place only one of the \"Plus 4\" card per turn.");
                        str.AppendLine("It doesn't matter what the last placed card is when it's a plus 4 card that's going to be placed next.");
                        str.AppendLine("You are allowed to place any number of `Skip`, `Reverse` and `Plus 2` cards.");
                        str.AppendLine("The flow of the game gets affected by how the `Skip` and `Reverse`cards are positioned upon placement.");
                        str.AppendLine("A Plus 4 card must end with one or multiple Numer Cards of the same label with same or different colors.\n");
                        str.AppendLine("__Example 1:__ Placing `Plus 4` > `Skip` > `9 Blue`\nOne turn will be skipped and the target players draws 4 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 2:__ Placing `Plus 4` > `Reverse` > `9 Blue`\nChanges the direction of the flow of the game and the previous player draws 4 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 3:__ Placing `Plus 4` > `Plus 2` > `9 Blue`\nThe next player draws 6 cards and the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 4:__ Placing `Plus 4` > `Plus 2` > `Skip` > `9 Blue`\nSkips one turn and makes the target player draw 6 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 5:__ Placing `Plus 4` > `Plus 2` > `Reverse` > `9 Blue`\nReverse the game direction and makes the previous player draw 6 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 6:__ Placing `Plus 4` > `Plus 2` > `Skip` > `9 Blue`\nReverse the game direction and makes the previous player draw 6 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 7:__ Placing `Plus 4` > `Plus 2` > `Skip` > `Reverse` > `9 Blue`\nSkips a turn and reverses the direction, which brings it back to you and forces you to draw 6 cards with the last card placed becomes 9 Blue.\n");
                        str.AppendLine("__Example 8:__ Placing `Plus 4` > `Plus 2` > `Reverse` > `Skip` > `9 Blue`\nReverses the direction and skips the previous player's turn, making the targeted player draw 6 cards with the last card placed becomes 9 Blue.\n");
                        break;

                    default:
                        str.AppendLine("CardName can be in the following below:");
                        str.AppendLine("```");
                        str.AppendLine("n  — Number Card");
                        str.AppendLine("r  — Reverse Card");
                        str.AppendLine("s  — Skip Card");
                        str.AppendLine("w  — Wild Card");
                        str.AppendLine("p2 — Plus 2 Card");
                        str.AppendLine("p4 — Plus 4 Card");
                        str.AppendLine("```");
                        break;
                }
            }

            await Context.User.SendConfirmAsync(str.ToString());
        }
    }
}