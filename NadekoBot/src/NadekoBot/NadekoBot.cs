﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Services;
using NadekoBot.Services.Impl;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using NadekoBot.Modules.Permissions;
using NadekoBot.TypeReaders;
using System.Collections.Concurrent;
using NadekoBot.Modules.Music;
using NadekoBot.Services.Database.Models;

namespace NadekoBot
{
    public class NadekoBot
    {
        private Logger _log;
        
        public static Color OkColor { get; }
        public static Color ErrorColor { get; }

        public static CommandService CommandService { get; private set; }
        public static CommandHandler CommandHandler { get; private set; }
        public static DiscordShardedClient Client { get; private set; }
        public static BotCredentials Credentials { get; private set; }

        public static GoogleApiService Google { get; private set; }
        public static StatsService Stats { get; private set; }

        public static ConcurrentDictionary<string, string> ModulePrefixes { get; private set; }
        public static bool Ready { get; private set; }

        public static IEnumerable<GuildConfig> AllGuildConfigs { get; }
        public static BotConfig BotConfig { get; }

        static NadekoBot()
        {
            SetupLogger();
            Credentials = new BotCredentials();

            using (var uow = DbHandler.UnitOfWork())
            {
                AllGuildConfigs = uow.GuildConfigs.GetAllGuildConfigs();
                BotConfig = uow.BotConfig.GetOrCreate();
                OkColor = new Color(Convert.ToUInt32(BotConfig.OkColor, 16));
                ErrorColor = new Color(Convert.ToUInt32(BotConfig.ErrorColor, 16));
            }
        }

        public async Task RunAsync(params string[] args)
        {
            _log = LogManager.GetCurrentClassLogger();

            _log.Info("Starting NadekoBot v" + StatsService.BotVersion);

            //create client
            Client = new DiscordShardedClient(new DiscordSocketConfig
            {
                AudioMode = Discord.Audio.AudioMode.Outgoing,
                MessageCacheSize = 10,
                LogLevel = LogSeverity.Warning,
                TotalShards = Credentials.TotalShards,
                ConnectionTimeout = int.MaxValue
            });
#if GLOBAL_NADEKO
            Client.Log += Client_Log;
#endif

            //initialize Services
            CommandService = new CommandService(new CommandServiceConfig() {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Sync
            });
            Google = new GoogleApiService();
            CommandHandler = new CommandHandler(Client, CommandService);
            Stats = new StatsService(Client, CommandHandler);

            ////setup DI
            //var depMap = new DependencyMap();
            //depMap.Add<ILocalization>(Localizer);
            //depMap.Add<ShardedDiscordClient>(Client);
            //depMap.Add<CommandService>(CommandService);
            //depMap.Add<IGoogleApiService>(Google);


            //setup typereaders
            CommandService.AddTypeReader<PermissionAction>(new PermissionActionTypeReader());
            CommandService.AddTypeReader<CommandInfo>(new CommandTypeReader());
            CommandService.AddTypeReader<ModuleInfo>(new ModuleTypeReader());
            CommandService.AddTypeReader<IGuild>(new GuildTypeReader());

            //connect
            await Client.LoginAsync(TokenType.Bot, Credentials.Token).ConfigureAwait(false);
            await Client.ConnectAsync().ConfigureAwait(false);
            //await Client.DownloadAllUsersAsync().ConfigureAwait(false);
            Stats.Initialize();

            _log.Info("Connected");

            //load commands and prefixes

            ModulePrefixes = new ConcurrentDictionary<string, string>(NadekoBot.BotConfig.ModulePrefixes.OrderByDescending(mp => mp.Prefix.Length).ToDictionary(m => m.ModuleName, m => m.Prefix));

            // start handling messages received in commandhandler
            
            await CommandHandler.StartHandling().ConfigureAwait(false);

            await CommandService.AddModulesAsync(this.GetType().GetTypeInfo().Assembly).ConfigureAwait(false);
#if !GLOBAL_NADEKO
            await CommandService.AddModuleAsync<Music>().ConfigureAwait(false);
#endif
            Ready = true;
            Console.WriteLine(await Stats.Print().ConfigureAwait(false));
        }

        private Task Client_Log(LogMessage arg)
        {
            _log.Warn(arg.Source + " | " + arg.Message);
            if (arg.Exception != null)
                _log.Warn(arg.Exception);

            return Task.CompletedTask;
        }

        public async Task RunAndBlockAsync(params string[] args)
        {
            await RunAsync(args).ConfigureAwait(false);
            await Task.Delay(-1).ConfigureAwait(false);
        }

        private static void SetupLogger()
        {
            try
            {
                var logConfig = new LoggingConfiguration();
                var consoleTarget = new ColoredConsoleTarget();

                consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} | ${message}";

                logConfig.AddTarget("Console", consoleTarget);

                logConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

                LogManager.Configuration = logConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
