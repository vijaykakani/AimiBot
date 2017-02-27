using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using Services.CleverBotApi;

namespace NadekoBot.Services.CleverBotApi
{
    public class CleverbotCustom
    {
        private string _cleverBotDomain = "http://www.cleverbot.com/";
        private SocketUserMessage _socketMessage;

        public CleverbotCustom(SocketUserMessage msg)
        {
            _socketMessage = msg;
        }

        public async Task<string> GetReply()
        {
            ITextChannel channel = _socketMessage.Channel as ITextChannel;
            var fullQueryLink = $"{_cleverBotDomain}getreply?key={NadekoBot.Credentials.CleverbotApiKey}&input={Uri.EscapeUriString(_socketMessage.Content)}";
            var reply = "";

            var httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.GetAsync(fullQueryLink);
            var contents = await response.Content.ReadAsStringAsync();

            List<string> list = contents.Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
            try
            {
                foreach (var par in list)
                {
                    var str = par.Replace("\"", string.Empty).Trim();
                    var str_list = str.Trim().Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (str_list[0] == "output")
                    {
                        reply = str_list[1];
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
