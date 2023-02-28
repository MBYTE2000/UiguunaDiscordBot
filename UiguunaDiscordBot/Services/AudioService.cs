using Discord;
using Discord.Audio;
using Google.Cloud.TextToSpeech.V1;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UiguunaDiscordBot.Services
{
    public class AudioService
    {
        private static readonly ConcurrentDictionary<ulong, AudioClient> _channels = new ConcurrentDictionary<ulong, AudioClient>();
        private static TextToSpeechClient _google;
        public AudioService()
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "uiguuna-google-auth.json");
        }

        public class AudioQueue
        {
            private static readonly ConcurrentQueue<AudioInQueue> _queue = new ConcurrentQueue<AudioInQueue>();
            private static bool _taskIsRunning = false;
            private static CancellationTokenSource _cancel;


            private class AudioInQueue
            {
                public ulong Server { get; set; }
                public string Message { get; set; }
                public AudioType Type { get; set; }
            }

            public enum AudioType
            {
                TTS,
                Audio
            }

            public void Enqueue(ulong serverId, string message, AudioType type)
            {
                _queue.Enqueue(new AudioInQueue {  Server = serverId, Message = message, Type = type });
                if (!_taskIsRunning)
                    Task.Run(ProcessQueuedItemsAsync);
            }

            public async void SkipQueue(ITextChannel channel)
            {
                if(_cancel != null)
                    _cancel.Cancel();
                else
                    await channel.SendMessageAsync("Nothing to skip");
            }

            private static async Task ProcessQueuedItemsAsync() 
            {
                while(true) 
                {
                    _taskIsRunning = true;
                    AudioInQueue item;
                    if(_queue.TryDequeue(out item)) 
                    {
                        _cancel = new CancellationTokenSource();
                        try 
                        {
                            if (item.Type == AudioType.TTS)
                                await ProcessTextToSpeach(item.Server, item.Message, _cancel.Token);
                            else
                                await ProcessPlayURL(item.Server, item.Message, _cancel.Token);
                        } 
                        catch (Exception ex) 
                        {
                            await Console.Out.WriteLineAsync(ex.Message);
                        }
                        finally { _cancel.Dispose(); }
                    }
                }
                _taskIsRunning = false;
            }
        }

        private class AudioClient 
        {
            public ulong ChannelId { get; set; }
            public IAudioClient Client { get; set; }
            public AudioOutStream AudioDevice { get; set; }
            public AudioQueue Queue { get; set; }
        }

        public async Task AddQueue(IGuild server, string message, AudioQueue.AudioType type) 
        {
            AudioClient voice;
            if(_channels.TryGetValue(server.Id, out voice))
                voice.Queue.Enqueue(server.Id, message, type);

            await Task.CompletedTask;
        }

        public async Task SkipAudio(IGuild server, ITextChannel textChannel)
        {
            AudioClient voice;
            if (_channels.TryGetValue(server.Id, out voice))
                voice.Queue.SkipQueue(textChannel);

            await Task.CompletedTask;
        }

        public async Task JoinAudio(IGuild server, IVoiceChannel voiceChannel, ITextChannel textChannel)
        {
            if (voiceChannel.Guild.Id != server.Id) return;
            AudioClient voice;

            if (_channels.TryGetValue(server.Id, out voice)) 
            {
                if(voiceChannel.Id == voice.ChannelId && voice.Client.ConnectionState == ConnectionState.Connected) 
                {
                    await textChannel.SendMessageAsync("Connected to the same channel.");
                    return;
                }
                var oldVoice = voice;
                var client = await voiceChannel.ConnectAsync();
                voice.AudioDevice = null;
                voice.Client = client;
                voice.ChannelId = voiceChannel.Id;
                _channels.TryUpdate(server.Id, voice, oldVoice);
                return;
            }

            voice = new AudioClient
            {
                ChannelId = voiceChannel.Id,
                Queue = new AudioQueue()
            };

            var audioClient = await voiceChannel.ConnectAsync();
            voice.Client = audioClient;
            if(!_channels.TryAdd(server.Id, voice))
                await textChannel.SendMessageAsync("Fail to add");
        }

        public async Task LeaveAudio(IGuild server)
        {
            AudioClient voice;
            if(_channels.TryRemove(server.Id, out voice))
                voice.Client.Dispose();

            await Task.CompletedTask;
        }

        private static string MD5(string input) 
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
                return Convert.ToHexString(hashBytes);
            }
        }

        private static async Task ProcessTextToSpeach(ulong server, string message, CancellationToken token)
        {
            if (!Directory.Exists("tts"))
                Directory.CreateDirectory("tts");

            string file_name = Path.Combine("tts", string.Format("{0}.mp3", MD5(message.ToLower())));

            if(!File.Exists(file_name))
            {
                if (_google == null)
                    _google = await TextToSpeechClient.CreateAsync();

                var response = await _google.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
                {
                    Input = new SynthesisInput { Text = message },
                    Voice = new VoiceSelectionParams { LanguageCode = "ru-Ru", Name = "ru-RU-Wavenet-C" },
                    AudioConfig = new AudioConfig { AudioEncoding = AudioEncoding.Mp3 }
                });

                using(var stream = File.Create(file_name))
                    response.AudioContent.WriteTo(stream);
            }

            await SendMp3AudioAsync(server, file_name, token);
        }
        private static async Task ProcessPlayURL(ulong server, string url, CancellationToken token) 
        {
            await SendMp3AudioAsync(server, url+".mp3", token);
        }

        private static async Task SendMp3AudioAsync(ulong server, string file_name, CancellationToken token)
        {
            AudioClient voice;
            if (!File.Exists(file_name)) return;

            if (_channels.TryGetValue(server, out voice))
            {
                using (var audio = new Mp3FileReader(file_name))
                {
                    var stream = new WaveFormatConversionStream(new WaveFormat(48000, 16, 2), audio);

                    if (voice.AudioDevice == null)
                        voice.AudioDevice = voice.Client.CreatePCMStream(AudioApplication.Mixed, 98304, 200);

                    try { await stream.CopyToAsync(voice.AudioDevice, 1920, token); }
                    finally {await voice.AudioDevice.FlushAsync();}
                }
            }
        }
    }
}
