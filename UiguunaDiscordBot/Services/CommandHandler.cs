using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Threading.Channels;

namespace UiguunaDiscordBot.Services
{
    public class CommandHandler : DiscordClientService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _service;
        private readonly IConfiguration _configuration;

        public CommandHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger, IConfiguration configuration, IServiceProvider provider, CommandService service)
            : base(client, logger)
        {
            _provider = provider;
            _client = client;
            _service = service;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.MessageReceived += OnMessageReceived;
            _service.CommandExecuted += OnCommandExecuted;
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        private async Task OnCommandExecuted(Optional<CommandInfo> optional, ICommandContext commandContext, Discord.Commands.IResult result)
        {
            if (result.IsSuccess)
            {
                return;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            await Console.Out.WriteLineAsync(result.ErrorReason);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private async Task OnMessageReceived(SocketMessage socketMessage)
        {
            if (!(socketMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            var argPos = 0;
            if (!message.HasStringPrefix(_configuration["Prefix"], ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;
            await Console.Out.WriteLineAsync($"{message.Author.Username}: {message.Content}");
            var context = new SocketCommandContext(this.Client, message);
            await _service.ExecuteAsync(context, argPos, _provider);
        }

    }
}