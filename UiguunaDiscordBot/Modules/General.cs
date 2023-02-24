using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UiguunaDiscordBot.Modules
{
    public class General : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        public async Task PingAsync() 
        {
            await this.Context.Channel.SendMessageAsync("pong");
        } 

        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinChannel(IVoiceChannel channel = null)
        {
            channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("User must be in a voice channel, or a voice channel must be passed as an argument."); return; }
            var audioClient = await channel.ConnectAsync();
        }
    }
}
