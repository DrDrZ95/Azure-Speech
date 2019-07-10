using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace speechtotextwpf
{
    /// <summary>
    /// 此类将获取对话回复。
    /// </summary>
    public class MSBot
    {
        public static string ErrorMsg = "糟糕，今天的网络有点差，我不知道该说什么了。。";
        public static string BotConnector = "lDqoeOl0HNQ.cwA.LOc.0kIUR3ta7zLUFkgKMejiDDQCmaheX0bf6oR_-HidbMw";

        public async Task<string> TalkMessage(string message)
        {
            #region 根据message说话内容获取Bot+Luis的回复结果
            try
            {

                HttpClient client;
                HttpResponseMessage response;

                string ReceivedString = null;

                client = new HttpClient();
                client.BaseAddress = new Uri("https://directline.botframework.com/v3/directline/");

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //BotConnector 为 Bot应用Direct Line的密钥
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BotConnector);

                var timeSpan = new TimeSpan(0, 0, 3);
                client.Timeout = timeSpan;

                ObjectCache cache = MemoryCache.Default;

                var conversationId = cache["conversationId"] as string;
                if (conversationId == null)
                {
                    var conversation = new Conversation();
                    response = await client.PostAsJsonAsync("conversations", conversation);

                    if (response.IsSuccessStatusCode)
                    {
                        ConversationModel ConversationInfo = response.Content.ReadAsAsync(typeof(ConversationModel)).Result as ConversationModel;
                        conversationId = ConversationInfo.conversationId;
                    }
                    else
                    {
                        return ErrorMsg;
                    }

                    CacheItemPolicy policy = new CacheItemPolicy();
                    policy.AbsoluteExpiration =
                        DateTimeOffset.Now.AddMinutes(28.0);

                    List<string> filePaths = new List<string>();

                    string cachedFilePath =
                        System.AppDomain.CurrentDomain.BaseDirectory + @"\savekey.txt";

                    filePaths.Add(cachedFilePath);
                    policy.ChangeMonitors.Add(new
                        HostFileChangeMonitor(filePaths));

                    File.WriteAllText(cachedFilePath, conversationId);
                    conversationId = File.ReadAllText(cachedFilePath);

                    cache.Set("conversationId", conversationId, policy);
                }

                string conversationUrl = "conversations/" +
                    conversationId + "/activities";

                var fromModel = new From() { id = "user1" };
                SendMessageModel msg = new SendMessageModel() { type = "message", from = fromModel, text = message };

                try
                {
                    response = await client.PostAsJsonAsync(conversationUrl, msg);
                }
                catch (Exception ex)
                {
                    return ErrorMsg;
                }

                ActivitiesConversation cBotMessage = response.Content.ReadAsAsync(typeof(ActivitiesConversation)).Result as ActivitiesConversation;
                var d = cBotMessage.id;

                if (response.IsSuccessStatusCode)
                {
                    response = await client.GetAsync(conversationUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        Activities BotMessage = response.Content.ReadAsAsync(typeof(Activities)).Result as Activities;

                        if (BotMessage.activities.Length > 1)
                        {
                            ReceivedString = BotMessage.activities[BotMessage.activities.Length - 1].text;
                        }

                        if (message == ReceivedString)
                        {
                            ReceivedString = "抱歉，我不知道你在说什么T_T，\n" +
                                        "您可以问我\n" +
                                        "【1.哪里天气怎么样】\n" +
                                        "【2.日常用语】\n" +
                                        "【3.讲个笑话】";
                        }
                    }
                }
                return ReceivedString;
            }
            catch (TimeoutException tex)
            {
                return ErrorMsg;
            }
            catch (Exception ex)
            {
                return ErrorMsg;
            }
            #endregion
        }
        
        #region 配置V3 directline的模型
        public class Conversation
        {
            public string conversationId { get; set; }
            public string token { get; set; }
            public string eTag { get; set; }
        }

        public class ConversationModel
        {
            public string conversationId { get; set; }
            public string token { get; set; }
            public string expires_in { get; set; }
            public string streamUrl { get; set; }
            public string referenceGrammarId { get; set; }
        }

        public class SendMessageModel
        {
            public string type { get; set; }
            public From from { get; set; }
            public string text { get; set; }
        }

        public class From
        {
            public string id { get; set; }
        }

        public class ActivitiesConversation
        {
            public string id { get; set; }
        }

        public class Activities
        {
            public ActivitiesMsg[] activities { get; set; }
            public string watermark { get; set; }
        }

        public class ActivitiesMsg
        {
            public string type { get; set; }
            public string id { get; set; }
            public string timestamp { get; set; }
            public string serviceUrl { get; set; }
            public string channelId { get; set; }
            public From from { get; set; }
            public ActivitiesConversation conversation { get; set; }
            public string text { get; set; }
            public Attachments[] attachments { get; set; }
            public Entities[] entities { get; set; }
            public string replyToId { get; set; }
        }
        public class Attachments
        {

        }
        public class Entities
        {

        }

        #endregion
    }
}
