using Discord;
using Discord.Audio;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using UiguunaDiscordBot.Services;


namespace UiguunaDiscordBot.Modules
{
    //[Group("audio")]
    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly AudioService _audio;
        public AudioModule(IServiceProvider services)
        {
            _audio = services.GetRequiredService<AudioService>();
        }


        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinAsync(IVoiceChannel channel = null) 
        {
            channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
            if (channel == null)
                await ReplyAsync("You must be in voice channel");
            else
                await _audio.JoinAudio(Context.Guild, channel, Context.Channel as ITextChannel);

            await _audio.AddQueue(Context.Guild, "", AudioService.AudioQueue.AudioType.Audio);
        }

        [Command("Leave", RunMode = RunMode.Async)]
        public async Task LeaveAsync() => await _audio.LeaveAudio(Context.Guild);

        [Command("skip", RunMode = RunMode.Async)]
        public async Task SkipAsync() => await _audio.SkipAudio(Context.Guild, Context.Channel as ITextChannel);

        [Command("say", RunMode = RunMode.Async)]
        public async Task SayAsync([Remainder] string textInput)
        {
            string message = Regex.Replace(textInput, @"@\\w[a-zA-Z0-9()]{0,75}#[0-9]{0,4}", "").Trim().ToLower();

            await Console.Out.WriteLineAsync(message);
            if (message.Length < 1)
            {
                await ReplyAsync("Invalid String");
                return;
            }

            await _audio.AddQueue(Context.Guild, message, AudioService.AudioQueue.AudioType.TTS);
        }
        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayAsync(string url)
        {
            await _audio.AddQueue(Context.Guild, url, AudioService.AudioQueue.AudioType.Audio);
        }
    }
}
