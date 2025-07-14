﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sisters.WudiLib.Posts;
using Sisters.WudiLib.Responses;

namespace Sisters.WudiLib
{
    partial class HttpApiClient
    {
        private ILogger _logger = Utilities.GetLogger<HttpApiClient>();
        
        private const string PrivatePath = "send_private_msg";
        private const string GroupPath = "send_group_msg";
        private const string DiscussPath = "send_discuss_msg";
        private const string MessagePath = "send_msg";
        private const string KickGroupMemberPath = "set_group_kick";
        private const string RecallPath = "delete_msg";
        private const string BanGroupMemberPath = "set_group_ban";
        private const string SetGroupCardPath = "set_group_card";
        private const string LoginInfoPath = "get_login_info";
        private const string GroupMemberInfoPath = "get_group_member_info";
        private const string GroupMemberListPath = "get_group_member_list";
        private const string CleanPath = "clean_data_dir";

        private string PrivateUrl => _apiAddress + PrivatePath;
        private string GroupUrl => _apiAddress + GroupPath;
        private string DiscussUrl => _apiAddress + DiscussPath;
        private string MessageUrl => _apiAddress + MessagePath;
        private string KickGroupMemberUrl => _apiAddress + KickGroupMemberPath;
        private string RecallUrl => _apiAddress + RecallPath;
        private string BanGroupMemberUrl => _apiAddress + BanGroupMemberPath;
        private string SetGroupCardUrl => _apiAddress + SetGroupCardPath;
        private string LoginInfoUrl => _apiAddress + LoginInfoPath;
        private string GroupMemberInfoUrl => _apiAddress + GroupMemberInfoPath;
        private string GroupMemberListUrl => _apiAddress + GroupMemberListPath;
        private string CleanUrl => _apiAddress + CleanPath;

        private HttpClient _httpClient = new HttpClient();
        private string _accessToken;

        /// <summary>
        /// API 访问 token。请详见插件文档。
        /// </summary>
        public virtual string AccessToken
        {
            get => _accessToken;
            set
            {
                if (_accessToken != value)
                {
                    var http = new HttpClient();
                    if (!string.IsNullOrEmpty(value))
                    {
                        http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse("Token " + value);
                    }
                    _httpClient = http;
                    _accessToken = value;
                }
            }
        }
    }

    /// <summary>
    /// 通过酷Q HTTP API实现QQ功能。
    /// </summary>
    public partial class HttpApiClient
    {
        /// <summary>
        /// 构造 <see cref="HttpApiClient"/> 的实例。
        /// </summary>
        public HttpApiClient()
        {

        }

        /// <summary>
        /// 构造 <see cref="HttpApiClient"/> 的实例，并指定
        /// <see cref="ApiAddress"/>。请注意在此指定的值不会受子类重载影响。
        /// </summary>
        /// <param name="apiAddress"></param>
        public HttpApiClient(string apiAddress) => PApiAddress = apiAddress;

        /// <summary>
        /// 构造 <see cref="HttpApiClient"/> 的实例，并指定 <see cref="ApiAddress"/> 和 <see cref="AccessToken"/>。
        /// </summary>
        public HttpApiClient(string apiAddress, string accessToken) : this(apiAddress)
            => AccessToken = accessToken;

        [Obsolete]
        private int _isReadyToCleanData;

        /// <summary>
        /// 是否已设置定期清理图片缓存。
        /// </summary>
        [Obsolete]
        public bool IsCleaningData => _isReadyToCleanData != 0;

        private string _apiAddress;

        private string PApiAddress
        {
            get => _apiAddress;
            set => _apiAddress = value.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? value : value + "/";
        }

        /// <summary>
        /// 获取或设置 HTTP API 的监听地址
        /// </summary>
        public virtual string ApiAddress
        {
            get => PApiAddress;
            set => PApiAddress = value;
        }

        /// <summary>
        /// 开始定期访问清理图片的 API。
        /// </summary>
        /// <param name="intervalMinutes">间隔的毫秒数。</param>
        /// <returns>成功开始则为 <c>true</c>，如果之前已经开始过，则为 <c>false</c>。</returns>
        [Obsolete]
        public bool StartClean(int intervalMinutes, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _isReadyToCleanData, 1, 0) == 0)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            await this.CleanImageData().ConfigureAwait(false);
                        }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                        catch (Exception)
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
                        {
                            // ignored
                        }
                        try
                        {
                            await Task.Delay(60000 * intervalMinutes, cancellationToken).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// 发送私聊消息。
        /// </summary>
        /// <param name="userId">对方 QQ 号。</param>
        /// <param name="message">要发送的内容（文本）。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendPrivateMessageResponseData> SendPrivateMessageAsync(long userId, string message)
        {
            var data = new
            {
                user_id = userId,
                message,
                auto_escape = true,
            };
            var result = await PostAsync<SendPrivateMessageResponseData>(PrivateUrl, data);
            _logger.LogInformation("[Message ID: {0}]Sent message to user {1}: {2}", result.MessageId, userId, message);
            return result;
        }

        /// <summary>
        /// 发送私聊消息。
        /// </summary>
        /// <param name="qq">对方 QQ 号。</param>
        /// <param name="message">要发送的内容。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendPrivateMessageResponseData> SendPrivateMessageAsync(long qq, Message message)
        {
            var data = new
            {
                user_id = qq,
                message = message.Serializing,
            };
            var result = await PostAsync<SendPrivateMessageResponseData>(PrivateUrl, data);
            _logger.LogInformation("[Message ID: {0}]Sent message to user {1}: {2}", result.MessageId, qq, message);
            return result;
        }

        /// <summary>
        /// 发送群消息。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="message">要发送的内容（文本）。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendGroupMessageResponseData> SendGroupMessageAsync(long groupId, string message)
        {
            var data = new
            {
                group_id = groupId,
                message,
                auto_escape = true,
            };
            var result = await PostAsync<SendGroupMessageResponseData>(GroupUrl, data);
            _logger.LogInformation("[Message ID: {0}]Sent message to group {1}: {2}", result.MessageId, groupId, message);
            return result;
        }

        /// <summary>
        /// 发送群消息。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="message">要发送的内容。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendGroupMessageResponseData> SendGroupMessageAsync(long groupId, Message message)
        {
            var data = new
            {
                group_id = groupId,
                message = message.Serializing,
            };
            var result = await PostAsync<SendGroupMessageResponseData>(GroupUrl, data);
            _logger.LogInformation("[Message ID: {0}]Sent message to group {1}: {2}", result.MessageId, groupId, message);
            return result;
        }

        /// <summary>
        /// 发送讨论组消息。
        /// </summary>
        /// <param name="discussId">讨论组 ID。</param>
        /// <param name="message">要发送的内容（文本）。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendDiscussMessageResponseData> SendDiscussMessageAsync(long discussId, string message)
        {
            var data = new
            {
                discuss_id = discussId,
                message,
                auto_escape = true,
            };
            var result = await PostAsync<SendDiscussMessageResponseData>(DiscussUrl, data);
            _logger.LogInformation("[Message ID: {0}]Sent message to discuss {1}: {2}", result.MessageId, discussId, message);
            return result;
        }

        /// <summary>
        /// 发送讨论组消息。
        /// </summary>
        /// <param name="discussId">讨论组 ID。</param>
        /// <param name="message">要发送的内容。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendDiscussMessageResponseData> SendDiscussMessageAsync(long discussId, Message message)
        {
            var data = new
            {
                discuss_id = discussId,
                message = message.Serializing,
            };
            var result = await PostAsync<SendDiscussMessageResponseData>(DiscussUrl, data);
            _logger.LogInformation("[Message ID: {0}]Sent message to discuss {1}: {2}", result.MessageId, discussId, message.Raw);

            return result;
        }

        /// <summary>
        /// 发送消息。
        /// </summary>
        /// <param name="endpoint">要发送到的终结点。</param>
        /// <param name="message">要发送的消息。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendMessageResponseData> SendMessageAsync(Posts.Endpoint endpoint, Message message)
        {
            var data = JObject.FromObject(endpoint);
            data["message"] = JToken.FromObject(message.Serializing);
            var result = await PostAsync<SendMessageResponseData>(MessageUrl, data);
            switch (endpoint)
            {
                case PrivateEndpoint privateEndpoint:
                    _logger.LogInformation("[Message ID: {0}]Sent message to user {1}: {2}", result.MessageId, privateEndpoint.UserId, message.Raw);
                    break;
                case GroupEndpoint groupEndpoint:
                    _logger.LogInformation("[Message ID: {0}]Sent message to group {1}: {2}", result.MessageId, groupEndpoint.GroupId, message.Raw);
                    break;
                case DiscussEndpoint discussEndpoint:
                    _logger.LogInformation("[Message ID: {0}]Sent message to discuss {1}: {2}", result.MessageId, discussEndpoint.DiscussId, message.Raw);
                    break;
                default:
                    _logger.LogInformation("[Message ID: {0}]Sent message: {1}", result.MessageId, message.Raw);
                    break;
            }
            return result;
        }

        /// <summary>
        /// 发送消息。
        /// </summary>
        /// <param name="endpoint">要发送到的终结点。</param>
        /// <param name="message">要发送的消息（文本）。</param>
        /// <returns>包含消息 ID 的响应数据。</returns>
        public async Task<SendMessageResponseData> SendMessageAsync(Posts.Endpoint endpoint, string message)
        {
            var data = JObject.FromObject(endpoint);
            data["message"] = JToken.FromObject(message);
            data["auto_escape"] = true;
            var result = await PostAsync<SendMessageResponseData>(MessageUrl, data);
            switch (endpoint)
            {
                case PrivateEndpoint privateEndpoint:
                    _logger.LogInformation("[Message ID: {0}]Sent message to user {1}: {2}", result.MessageId, privateEndpoint.UserId, message);
                    break;
                case GroupEndpoint groupEndpoint:
                    _logger.LogInformation("[Message ID: {0}]Sent message to group {1}: {2}", result.MessageId, groupEndpoint.GroupId, message);
                    break;
                case DiscussEndpoint discussEndpoint:
                    _logger.LogInformation("[Message ID: {0}]Sent message to discuss {1}: {2}", result.MessageId, discussEndpoint.DiscussId, message);
                    break;
                default:
                    _logger.LogInformation("[Message ID: {0}]Sent message: {1}", result.MessageId, message);
                    break;
            }
            return result;
        }

        /// <summary>
        /// 群组踢人。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="userId">要踢的 QQ 号。</param>
        /// <returns>是否成功。注意：酷 Q 未处理错误，所以无论是否成功都会返回<c>true</c>。</returns>
        public Task<bool> KickGroupMemberAsync(long groupId, long userId)
        {
            var data = new
            {
                group_id = groupId,
                user_id = userId,
            };
            _logger.LogInformation("Kicked user {0} from group {1}", userId, groupId);
            return PostAsync(KickGroupMemberUrl, data);
        }

        /// <summary>
        /// 撤回消息（需要Pro）。
        /// </summary>
        /// <param name="message">消息返回值。</param>
        /// <returns>是否成功。</returns>
        public Task<bool> RecallMessageAsync(SendMessageResponseData message)
            => RecallMessageAsync(message.MessageId);

        /// <summary>
        /// 撤回消息（需要Pro）
        /// </summary>
        /// <param name="messageId">消息 ID。</param>
        /// <returns>是否成功。</returns>
        public Task<bool> RecallMessageAsync(int messageId)
        {
            var data = new { message_id = messageId };
            _logger.LogInformation("Recalled message {0}", messageId);
            return PostAsync(RecallUrl, data);
        }

#nullable enable
        /// <summary>
        /// 获取消息
        /// </summary>
        /// <param name="messageId">消息 ID</param>
        /// <returns></returns>
        public async Task<GetMessageResponseData> GetMessage(int messageId)
        {
            var data = new { message_id = messageId };
            var result = await CallAsync<GetMessageResponseData>("get_msg", data);
            _logger.LogInformation("[Message ID: {0}]Get message from {1}: {2}", messageId, $"{result.Sender.Nickname}({result.Sender.UserId})", result.Message.Raw);
            return result;
        }
#nullable restore

        /// <summary>
        /// 群组单人禁言。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="userId">要禁言的 QQ 号。</param>
        /// <param name="duration">禁言时长，单位秒，0 表示取消禁言。</param>
        /// <exception cref="ApiAccessException"></exception>
        /// <returns>如果操作成功，返回 <c>true</c>。</returns>
        public Task<bool> BanGroupMember(long groupId, long userId, int duration)
        {
            var data = new
            {
                group_id = groupId,
                user_id = userId,
                duration,
            };
            _logger.LogInformation("Banned user {0} from group {1} for {2} seconds", userId, groupId, duration);
            return PostAsync(BanGroupMemberUrl, data);
        }

        /// <summary>
        /// 自动识别发送者类型（普通/匿名）并禁言。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="messageSource">群消息上报的 <see cref="Posts.Message.Source"/> 属性。</param>
        /// <param name="duration">禁言时长，单位秒，0 表示取消禁言，无法取消匿名用户禁言。</param>
        /// <returns>如果操作成功，返回 <c>true</c>。</returns>
        public Task<bool> BanMessageSource(long groupId, MessageSource messageSource, int duration)
        {
            ArgumentNullException.ThrowIfNull(messageSource);
            return messageSource.IsAnonymous
                ? BanAnonymousMember(groupId, messageSource.AnonymousFlag, duration)
                : BanGroupMember(groupId, messageSource.UserId, duration);
        }

        /// <summary>
        /// 群组匿名用户禁言。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="flag">要禁言的匿名用户的 flag。</param>
        /// <param name="duration">禁言时长，单位秒，无法取消匿名用户禁言。</param>
        /// <returns>如果操作成功，返回 <c>true</c>。</returns>
        public Task<bool> BanAnonymousMember(long groupId, string flag, int duration)
        {
            var data = new
            {
                group_id = groupId,
                anonymous_flag = flag,
                duration,
            };
            _logger.LogInformation("Banned anonymous user {0} from group {1} for {2} seconds", flag, groupId, duration);
            return CallAsync("set_group_anonymous_ban", data);
        }

        /// <summary>
        /// 群组匿名用户禁言。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="anonymousInfo">要禁言的匿名用户对象（<see cref="AnonymousMessage.Anonymous"/> 属性）。</param>
        /// <param name="duration">禁言时长，单位秒，无法取消匿名用户禁言。</param>
        /// <returns>如果操作成功，返回 <c>true</c>。</returns>
        public Task<bool> BanAnonymousMember(long groupId, AnonymousInfo anonymousInfo, int duration)
        {
            ArgumentNullException.ThrowIfNull(anonymousInfo);
            return BanAnonymousMember(groupId, anonymousInfo.Flag, duration);
        }

        /// <summary>
        /// 群组全员禁言。
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public Task<bool> BanWholeGroup(long groupId, bool enable)
        {
            var data = new
            {
                group_id = groupId,
                enable,
            };
            _logger.LogInformation("{0} whole group {1}", enable ? "Banned" : "Unbanned", groupId);
            return CallAsync("set_group_whole_ban", data);
        }

        /// <summary>
        /// 设置群名片。
        /// </summary>
        /// <param name="groupId">群号。</param>
        /// <param name="userId">要设置的 QQ 号。</param>
        /// <param name="card">群名片内容，不填或空字符串表示删除群名片。</param>
        /// <returns>是否成功。</returns>
        public Task<bool> SetGroupCard(long groupId, long userId, string card)
        {
            var data = new
            {
                group_id = groupId,
                user_id = userId,
                card,
            };
            _logger.LogInformation("Set group card of {0} to {1}", userId, card);
            return PostAsync(SetGroupCardUrl, data);
        }

        // set_group_name
        public Task<bool> SetGroupName(long groupId, string groupName)
        {
            var data = new
            {
                group_id = groupId,
                group_name = groupName,
            };
            _logger.LogInformation("Set group name of {0} to {1}", groupId, groupName);
            return CallAsync("set_group_name", data);
        }

        // set_group_leave
        public Task<bool> LeaveGroup(long groupId, bool isDismiss = false)
        {
            var data = new
            {
                group_id = groupId,
                is_dismiss = isDismiss,
            };
            _logger.LogInformation("{0} left group {1}", isDismiss ? "Dismissed" : "Left", groupId);
            return CallAsync("set_group_leave", data);
        }

        // set_group_special_title
        public Task<bool> SetGroupSpecialTitle(long groupId, long userId, string specialTitle = "", int duration = -1)
        {
            var data = new
            {
                group_id = groupId,
                user_id = userId,
                special_title = specialTitle,
                duration,
            };
            _logger.LogInformation("Set special title of {0} to {1} in group {2}", userId, specialTitle, groupId);
            return CallAsync("set_group_special_title", data);
        }

        // set_friend_add_request
        public Task<bool> SetFriendAddRequest(string flag, bool approve = true, string remark = "")
        {
            var data = new
            {
                flag,
                approve,
                remark,
            };
            _logger.LogInformation("{0} friend request {1}", approve ? "Approved" : "Rejected", flag);
            return CallAsync("set_friend_add_request", data);
        }

        /// <summary>
        /// 获取登录信息。
        /// </summary>
        /// <returns>登录信息。</returns>
        public Task<LoginInfo> GetLoginInfoAsync()
        {
            var data = new object();
            return PostAsync<LoginInfo>(LoginInfoUrl, data);
        }

        /// <summary>
        /// 获取好友列表。
        /// </summary>
        /// <returns>好友数组。</returns>
        public async Task<Friend[]> GetFriendListAsync()
        {
            var result = await CallAsync<Friend[]>("get_friend_list", new object());
            _logger.LogInformation("Got {0} friends", result.Length);
            return result;
        }

        /// <summary>
        /// 获取群列表。
        /// </summary>
        /// <returns></returns>
        public async Task<GroupInfo[]> GetGroupListAsync()
        {
            var result = await CallAsync<GroupInfo[]>("get_group_list", new object());
            _logger.LogInformation("Got {0} groups", result.Length);
            return result;
        }

        /// <summary>
        /// 获取群成员信息。
        /// </summary>
        /// <param name="group">群号。</param>
        /// <param name="qq">QQ 号（不可以是登录号）。</param>
        /// <returns>获取到的成员信息。</returns>
        public async Task<GroupMemberInfo> GetGroupMemberInfoAsync(long group, long qq)
        {
            var data = new
            {
                group_id = group,
                user_id = qq,
                no_cache = true,
            };
            var result = await PostAsync<GroupMemberInfo>(GroupMemberInfoUrl, data);
            _logger.LogInformation("Got group member info of {0} in group {1}", $"{result.Nickname}({result.UserId})", group);
            return result;
        }

        /// <summary>
        /// 获取群成员列表。
        /// </summary>
        /// <param name="group">群号。</param>
        /// <returns>响应内容为数组，每个元素的内容和上面的 GetGroupMemberInfoAsync() 方法相同，但对于同一个群组的同一个成员，获取列表时和获取单独的成员信息时，某些字段可能有所不同，例如 area、title 等字段在获取列表时无法获得，具体应以单独的成员信息为准。</returns>
        public async Task<GroupMemberInfo[]> GetGroupMemberListAsync(long group)
        {
            var data = new
            {
                group_id = group,
            };
            var result = await PostAsync<GroupMemberInfo[]>(GroupMemberListUrl, data);
            _logger.LogInformation("Got {0} group members in group {1}", result.Length, group);
            return result;
        }

        /// <summary>
        /// 获取插件运行状态。
        /// </summary>
        /// <returns></returns>
        public Task<Status> GetStatusAsync()
            => CallAsync<Status>("get_status", new object());

        /// <summary>
        /// 清理数据目录中的图片。经测试可能无效。
        /// </summary>
        /// <returns></returns>
        public Task CleanImageData()
            => PostAsync(CleanUrl, new { data_dir = "image" });

        #region 值得重载的基础方法。

        protected virtual HttpClient HttpClient => _httpClient;

        /// <summary>
        /// 调用 API，并返回反序列化后的 <see cref="JObject"/> 对象。默认情况下会调用
        /// <see cref="CallRawAsync(string, string)"/>，重写后也可以不调用。
        /// </summary>
        /// <param name="action">调用的 API，如 <c>send_msg</c>。</param>
        /// <param name="data">参数对象。</param>
        /// <exception cref="Exception">所有异常应由调用方处理。</exception>
        /// <returns>由响应字符串反序列化成的 <see cref="JObject"/> 对象。</returns>
        protected virtual async Task<JObject> CallRawJObjectAsync(string action, object data)
        {
            string json = JsonConvert.SerializeObject(data);
            string responseContent = await CallRawAsync(action, json).ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<JObject>(responseContent);
            return result;
        }

        /// <summary>
        /// 调用 API，返回响应字符串。是最底层的方法，被
        /// <see cref="CallRawJObjectAsync(string, object)"/> 使用。如果重写
        /// <see cref="CallRawJObjectAsync(string, object)"/>，也可以不调用此方法。
        /// </summary>
        /// <param name="action">调用的 API，如 <c>send_msg</c>。</param>
        /// <param name="json">序列化过的 JSON 参数。</param>
        /// <exception cref="Exception">所有异常应由调用方处理。</exception>
        /// <returns>响应的 JSON 字符串。</returns>
        protected virtual async Task<string> CallRawAsync(string action, string json)
        {
            using HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
            var http = HttpClient;
            using var response = (await http.PostAsync(_apiAddress + action, content).ConfigureAwait(false)).EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        #endregion

        #region Old Utilities (Use URL)

        private Task<CqHttpApiResponse<T>> PostApiAsync<T>(string url, object data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data), "data不能为null");
            var action = url.Substring(url.LastIndexOf('/') + 1);
            return CallApiAsync<T>(action, data);
        }

        /// <summary>
        /// 通过 POST 请求访问API，返回数据
        /// </summary>
        /// <typeparam name="T">返回的数据类型</typeparam>
        /// <param name="url">API请求地址</param>
        /// <param name="data">请求参数</param>
        /// <returns>从 HTTP API 返回的数据</returns>
        /// <exception cref="ApiAccessException">网络错误等。</exception>
        /// <exception cref="ArgumentNullException"><c>data</c> was null.</exception>
        private async Task<T> PostAsync<T>(string url, object data)
        {
            var response = await PostApiAsync<T>(url, data).ConfigureAwait(false);
            return response.Retcode == CqHttpApiResponse.RetcodeOK ? response.Data : default(T);
        }

        /// <exception cref="ApiAccessException">网络错误等。</exception>
        /// <exception cref="ArgumentNullException"><c>data</c> was null.</exception>
        private async Task<bool> PostAsync(string url, object data)
        {
            try
            {
                var response = await PostApiAsync<object>(url, data).ConfigureAwait(false);
                return response.Retcode == CqHttpApiResponse.RetcodeOK;
            }
            catch (AggregateException e)
            {
                // will it happen?
                System.Diagnostics.Debug.Fail("PostAsync throws an AggregateException.");
                throw e.InnerException;
            }
        }

        #endregion

        #region New Utilities (Use Action string)

        /// <summary>
        /// 传入 <c>action</c> 和 <c>data</c> 的方法。
        /// </summary>
        private async Task<CqHttpApiResponse<T>> CallApiAsync<T>(string action, object data)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("message", nameof(action));
            }

            ArgumentNullException.ThrowIfNull(data);

            try
            {
                var responseJObject = await CallRawJObjectAsync(action, data).ConfigureAwait(false);
                var result = responseJObject.ToObject<CqHttpApiResponse<T>>();
                return result;
            }
            catch (Exception e)
            {
                throw new ApiAccessException("访问 API 时出现错误。", e);
            }
        }

        /// <summary>
        /// 调用指定 API，并指定返回数据。
        /// </summary>
        /// <typeparam name="T">返回数据类型。</typeparam>
        /// <param name="action">要调用的 API 功能。</param>
        /// <param name="data">参数数据。</param>
        /// <returns>返回数据。如果不成功（但不是网络错误），则为 <c>default</c>。</returns>
        /// <exception cref="ApiAccessException">网络错误等。</exception>
        /// <exception cref="ArgumentNullException"><c>data</c> was null -or- <c>action</c> was null.</exception>
        public async Task<T> CallAsync<T>(string action, object data)
        {
            var response = await CallApiAsync<T>(action, data).ConfigureAwait(false);
            return response.Retcode == CqHttpApiResponse.RetcodeOK ? response.Data : default;
        }

        /// <summary>
        /// 调用指定 API，返回是否成功。
        /// </summary>
        /// <param name="action">要调用的 API 功能。</param>
        /// <param name="data">参数数据。</param>
        /// <returns>是否成功（<see cref="CqHttpApiResponse.IsAcceptableStatus"/> 为 <c>true</c>）。</returns>
        /// <exception cref="ApiAccessException">网络错误等。</exception>
        /// <exception cref="ArgumentNullException"><c>data</c> was null -or- <c>action</c> was null.</exception>
        public async Task<bool> CallAsync(string action, object data)
        {
            var response = await CallApiAsync<object>(action, data).ConfigureAwait(false);
            return response.IsAcceptableStatus;
        }

        #endregion
    }
}
