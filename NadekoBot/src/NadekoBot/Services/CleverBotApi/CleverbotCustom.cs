using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;

namespace NadekoBot.Services.CleverBotApi
{
    public class CleverbotCustom
    {
        private string _cleverBotDomain = "http://www.cleverbot.com/";
        private SocketUserMessage _socketMessage;
        private static Dictionary<ulong, string> SessionIDs { get; set; }

        public CleverbotCustom(SocketUserMessage msg)
        {
            _socketMessage = msg;

            if (SessionIDs == null)
                SessionIDs = new Dictionary<ulong, string>();

            var channel = msg.Channel as ITextChannel;
            if (!SessionIDs.ContainsKey(channel.Guild.Id))
                SessionIDs.Add(channel.Guild.Id, "");
        }

        public async Task<string> GetReply()
        {
            var channel = _socketMessage.Channel as ITextChannel;
            var fullQueryLink = $"{_cleverBotDomain}getreply?key={NadekoBot.Credentials.CleverbotApiKey}" + 
                                $"&input={Uri.EscapeUriString(_socketMessage.Content)}&cs={SessionIDs[channel.Guild.Id]}";
            var reply = "";

            var httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(fullQueryLink);
            var contents = await response.Content.ReadAsStringAsync();

            List<string> list = contents.Trim().Substring(0, contents.Length - 1).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
            try
            {
                foreach (var par in list)
                {
                    var str = par.Replace("\"", string.Empty).Trim();
                    var str_list = str.Trim().Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                    //  get and store the new session id
                    if (str_list[0].Trim() == "cs")
                    {
                        SessionIDs[channel.Guild.Id] = str_list[1];
                    }

                    //  read what the bot says
                    if (str_list[0].Trim() == "output")
                    {
                        reply = str_list[1].Trim();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await channel.SendErrorAsync(ex.Message);
            }

            return reply;
        }
    }
}
