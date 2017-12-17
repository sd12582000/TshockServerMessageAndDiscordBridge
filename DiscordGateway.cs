using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using WebSocketSharp;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace WebSocketTest
{
    public class DiscordGateway:IDisposable
    {
        private static HttpClient client = null;
        private static WebSocket ws = null;
        private static int s = 0;
        private string botToken = "";
        private int shards;
        private Thread HeartbeatACK = null;
        private static bool isStop = false;
        private static bool isDispose = false;
        private string session_id = "";

        public Action<string, string, string> ReceiveMessage;

        public DiscordGateway(string token)
        {
            botToken = token;
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", string.Format("Bot {0}",token));
            string wsUrl = getSocketUrlAsync().Result;
            ws = new WebSocket(string.Format("{0}?v=6&encoding=json", wsUrl));
            ws.OnOpen += Ws_OnOpen;
            ws.OnMessage += Ws_OnMessage;
            ws.OnError += Ws_OnError;
            ws.OnClose += Ws_OnClose;
        }
        

        public void Dispose()
        {
            if (!isDispose)
            {
                isDispose = true;
                client.Dispose();
                ws.Close();
                HeartbeatACK.Abort();
            }
        }

        public void connect()
        {
            ws.Connect();
        }

        public void SendMessage(string message , string channel = "385863472950411264")
        {
            string url = string.Format("https://discordapp.com/api/channels/{0}/messages", channel);
            string result = client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string> { { "content", message } })).Result.Content.ReadAsStringAsync().Result;
        }

        private void logMessage(string message)
        {
            Console.WriteLine(message);
        }

        #region OnEvent

        private void Ws_OnOpen(object sender, EventArgs e)
        {
            isStop = false;
            logMessage("Ws_OnOpen");
        }

        private void Ws_OnClose(object sender, CloseEventArgs e)
        {
            isStop = true;
            HeartbeatACK.Abort();
            //HeartbeatACK.Dispose();
            logMessage(string.Format("Ws_OnClose {0}", e.Reason));
            if (!isDispose)
            {
                connect();
            }
        }

        private void Ws_OnError(object sender, ErrorEventArgs e)
        {
            isStop = true;
            logMessage(string.Format("on error {0}", e.Message));
        }

        private void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            //logMessage(e.Data);
            var ws = sender as WebSocket;
            dynamic json = JValue.Parse(e.Data);
            if (json.s != null)
                s = Convert.ToInt32(json.s);
            
            //Hello
            if (json.op == 10)
            {
                
                logMessage("Hello");
                int interval = json.d.heartbeat_interval;
                //Start an heatBeat loop
                HeartbeatACK = new Thread(() => heartBeat(interval));
                HeartbeatACK.Start();
                SendIdentify();
            }
            //Heartbeat ACK
            else if (json.op == 11)
            {
            }
            else if(json.op == 7)
            {
                string id = "{\"op\": 6,\"d\": {\"token\": \"" + botToken + "\",\"session_id\""+session_id+ "\",\"seq\": "+ s .ToString()+ "}}";
                ws.Send(id);
            }
            //Invalid Session
            else if (json.op == 9)
            {
                SendIdentify();
            }
            //dispatches an event
            else if (json.op == 0)
            {
                if (json.t == "MESSAGE_CREATE")
                {
                    ReceiveMessage(string.Format("{0}",json.d.author.username), string.Format("{0}", json.d.content), string.Format( "{0}",json.d.channel_id));
                }
            }
        }
        

        #endregion

        private void SendIdentify()
        {
            string head = "{\"op\": 2,\"d\": {\"token\": \"";
            string tail = "\",\"properties\":{},\"compress\": false,\"large_threshold\": 250}}";

            string data = string.Format("{0}{1}{2}", head, botToken, tail);
            logMessage(data);
            ws.Send(data);
        }

        private static void heartBeat(int sleepTime)
        {
            while (ws.IsAlive&&!isStop)
            {
                Thread.Sleep(sleepTime);
                string id = string.Format("{0}\"op\": 1,\"d\": {1}{2}", '{', s, '}');
                ws.Send(id);
            }
        }

        private async Task<string> getSocketUrlAsync()
        {
            string apiurl = "https://discordapp.com/api/gateway/bot";
            HttpResponseMessage response = await client.GetAsync(apiurl);
            string jsonData = await response.Content.ReadAsStringAsync();
            dynamic jsonObject = JValue.Parse(jsonData);
            //Console.WriteLine(json.url);
            shards = jsonObject.shards;
            return jsonObject.url;
        }
    }
}
