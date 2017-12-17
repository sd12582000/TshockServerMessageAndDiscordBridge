using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using Terraria.DataStructures;

using System.Net.Http;
using System.Net.Http.Headers;

using System.IO;
using WebSocketTest;
using Newtonsoft.Json;

namespace TshockServerMessageAndDiscordBridge
{
    [ApiVersion(2, 1)]
    public class TshockServerMessageAndDiscordBridge : TerrariaPlugin
    {
        /// <summary>
        /// Gets the author(s) of this plugin
        /// </summary>
        public override string Author
        {
            get { return "OldNick"; }
        }

        /// <summary>
        /// Gets the description of this plugin.
        /// A short, one lined description that tells people what your plugin does.
        /// </summary>
        public override string Description
        {
            get { return "Server message(login, chat, death, etc...) And discord channel Bridge "; }
        }

        /// <summary>
        /// Gets the name of this plugin.
        /// </summary>
        public override string Name
        {
            get { return "ServerMessageAndDiscordBridge Plugin"; }
        }

        /// <summary>
        /// Gets the version of this plugin.
        /// </summary>
        public override Version Version
        {
            get { return new Version(1, 0, 0, 0); }
        }

        /// <summary>
        /// Initializes a new instance of the TestPlugin class.
        /// This is where you set the plugin's order and perfro other constructor logic
        /// </summary>
        public TshockServerMessageAndDiscordBridge(Main game) : base(game)
        {
            
        }

        /// <summary>
        /// Discord bot 收到訊息時的處理 
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="content"></param>
        /// <param name="channel_id"></param>
        private void GetMessageFromDiscord(string userName, string content, string channel_id)
        {
            //由機器人發出的訊息略過
            if(userName == Configs.BotConfig.DiscordBotName)
            {
                return;
            }

            //只拿指定的頻道訊息
            if(channel_id == Configs.BotConfig.DiscordCannelId)
            {
                TShockAPI.Utils.Instance.Broadcast(string.Format("{0}<{1}>):{2}", Config.DISCORD_BOT_PREIX_FORMAT, userName, content), 177, 91, 255);
            }

        }

        /// <summary>
        /// 如果出錯又跑出DiscordBot.ReceiveMessage 照理說不可能發生
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="content"></param>
        /// <param name="channel_id"></param>
        private void NoBotToken(string userName, string content, string channel_id)
        {
            TShockAPI.Utils.Instance.Broadcast("TshockServerMessageAndDiscordBridge Initialize Error", 255, 0, 0);
        }

        /// <summary>
        /// Handles plugin initialization. 
        /// Fired when the server is started and the plugin is being loaded.
        /// You may register hooks, perform loading procedures etc here.
        /// </summary>
        public override void Initialize()
        {
            if (Configs == null)
                Configs = new Config();

            if (Configs.BotConfig.LanguageID != 0)
            {
                Terraria.Localization.LanguageManager.Instance.LoadLanguage(Terraria.Localization.GameCulture.FromLegacyId(Configs.BotConfig.LanguageID));
            }
            else
            {
                Console.WriteLine("GameCulture Language Setting missing");
            }

            if (DiscordBot == null)
            {
                if (!string.IsNullOrEmpty(Configs.BotConfig.DiscordAppToken))
                {
                    DiscordBot = new DiscordGateway(Configs.BotConfig.DiscordAppToken);
                    DiscordBot.ReceiveMessage = GetMessageFromDiscord;
                    DiscordBot.connect();
                }
                else
                {
                    Console.WriteLine("Initialize Error DiscordAppToken not found");
                }
            }
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            ServerApi.Hooks.ServerBroadcast.Register(this, OnServerBroadcast);
            ServerApi.Hooks.WireTriggerAnnouncementBox.Register(this,OnTriggerAnnouncementBoxEvent);
            TShockAPI.Hooks.PlayerHooks.PlayerChat += OnPlayerChat;
        }

        /// <summary>
        /// Handles plugin disposal logic.
        /// *Supposed* to fire when the server shuts down.
        /// You should deregister hooks and free all resources here.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);
                ServerApi.Hooks.ServerBroadcast.Deregister(this, OnServerBroadcast);
                ServerApi.Hooks.WireTriggerAnnouncementBox.Deregister(this, OnTriggerAnnouncementBoxEvent);
                //ServerApi.Hooks.ServerChat.Deregister(this, OnServerChat);
                TShockAPI.Hooks.PlayerHooks.PlayerChat -= OnPlayerChat;
                DiscordBot.Dispose();
            }
            base.Dispose(disposing);
        }
        public static DiscordGateway DiscordBot = null;
        public static Config Configs = null;
        public class Config
        {
            public const string CHARACTER_NAME_FORMAT = "{character_name}";
            public const string MESSAGE_FORMAT = "{message}";
            public const string CURRENT_PLAYERS_FORMAT = "{current_players}";
            public const string SERVER_NAME_FORMAT = "{server_name}";
            public const string DISCORD_BOT_PREIX_FORMAT = "([g:5]Discord";
            public const string ANNOUNCEMENT_BOX_FORMAT = "廣播盒公告：{0}";

            public string CurrentPlayersMessageFormat = $"{CURRENT_PLAYERS_FORMAT} players now on {SERVER_NAME_FORMAT}.";
            public string LoginMessageFormat = $"{CHARACTER_NAME_FORMAT} has joined.";
            public string LogoutMessageFormat = $"{CHARACTER_NAME_FORMAT} has left.";
            public string ChatMessageFormat = $"<{CHARACTER_NAME_FORMAT}> {MESSAGE_FORMAT}";
            
            public DiscordConfig BotConfig = null;

            public Config()
            {
                string botConfigPath = Path.Combine(TShock.SavePath, "discord_bot_config.json");
                if (File.Exists(botConfigPath))
                { 
                    using (var reader = new StreamReader(botConfigPath))
                    {
                        string txt = reader.ReadToEnd();
                        BotConfig = JsonConvert.DeserializeObject<DiscordConfig>(txt);
                    }
                }
                else
                {
                    Console.WriteLine(string.Format("{0} not found", botConfigPath));
                    BotConfig = new DiscordConfig()
                    {
                        DiscordAppToken = string.Empty,
                        DiscordBotName = string.Empty,
                        DiscordCannelId = string.Empty
                    };
                }
            }

            public class DiscordConfig
            {
                public string DiscordAppToken = string.Empty;
                public string DiscordCannelId = string.Empty;
                public string DiscordBotName = string.Empty;
                public int LanguageID = 0;
            }
            
        }

        private void OnServerBroadcast(ServerBroadcastEventArgs args)
        {
            string message = args.Message.ToString();
            Console.WriteLine(args.Message._text);
            if (args.Message._text.StartsWith("DeathText"))
            {
                message = string.Format("(死掉了){0}", message);
            }

            PostMessageToDiscord(message);
        }

        /// <summary>
        /// 廣播盒鬼叫
        /// </summary>
        /// <param name="args"></param>
        private void OnTriggerAnnouncementBoxEvent(TriggerAnnouncementBoxEventArgs args)
        {
            string message = string.Format( Config.ANNOUNCEMENT_BOX_FORMAT,args.Text);
            PostMessageToDiscord(message);
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            TSPlayer player = TShock.Players[args.Who];
            if (player == null)
                return;
            string message = Configs.LoginMessageFormat.Replace(Config.CHARACTER_NAME_FORMAT, player.Name);

            List<string> players = TShock.Utils.GetPlayers(false);
            players.Add(player.Name);
            string currentPlayersMessage = Configs.CurrentPlayersMessageFormat.Replace(Config.CURRENT_PLAYERS_FORMAT, (TShock.Utils.ActivePlayers() + 1).ToString()).Replace(Config.SERVER_NAME_FORMAT, TShock.Config.ServerName);
            message = $"{message}\n{currentPlayersMessage}\n{String.Join(", ", players.ToArray())}";

            //Console.WriteLine("OnServerJoin: {0}", message);
            PostMessageToDiscord(message);
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            if (args.Who >= TShock.Players.Length || args.Who < 0)
            {
                //Something not right has happened
                return;
            }

            TSPlayer tsplr = TShock.Players[args.Who];
            if (tsplr == null || String.IsNullOrEmpty(tsplr.Name))
            {
                return;
            }

            string message = Configs.LogoutMessageFormat.Replace(Config.CHARACTER_NAME_FORMAT, tsplr.Name);

            List<string> players = TShock.Utils.GetPlayers(false);
            players.Remove(tsplr.Name);
            int activePlayers = TShock.Utils.ActivePlayers();
            if (0 < activePlayers)
                activePlayers--;
            string currentPlayersMessage = Configs.CurrentPlayersMessageFormat.Replace(Config.CURRENT_PLAYERS_FORMAT, (activePlayers).ToString()).Replace(Config.SERVER_NAME_FORMAT, TShock.Config.ServerName);
            message = $"{message}\n{currentPlayersMessage}\n{String.Join(", ", players.ToArray())}";

            //Console.WriteLine("OnServerLeave: {0}", message);
            PostMessageToDiscord(message);
        }


        private void OnPlayerChat(TShockAPI.Hooks.PlayerChatEventArgs args)
        {
            string message = Configs.ChatMessageFormat.Replace(Config.CHARACTER_NAME_FORMAT, args.Player.Name).Replace(Config.MESSAGE_FORMAT, args.RawText);
            //Console.WriteLine("OnPlayerChat: {0}", message);
            PostMessageToDiscord(message);
        }
        
        private void PostMessageToDiscord(string message)
        {
            //略過從DISCORD傳來的訊息
            if (message.StartsWith(Config.DISCORD_BOT_PREIX_FORMAT))
            {
                return;
            }

            //Console.WriteLine("start PostMessageToDiscord");
            if (!String.IsNullOrEmpty(Configs.BotConfig.DiscordAppToken) && !String.IsNullOrEmpty(Configs.BotConfig.DiscordCannelId))
            {
                Task.Run(() => DiscordBot.SendMessage(message,Configs.BotConfig.DiscordCannelId));
            }
            //Console.WriteLine("ends PostMessageToDiscord");
        }
    }
}
