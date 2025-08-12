﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Sisters.WudiLib.Posts
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Post
    {
        internal const string Message = "message";
        internal const string Notice = "notice";
        internal const string Request = "request";
        internal const string MetaEvent = "meta_event";

        internal const string TypeField = "post_type";
        internal const string SubTypeField = "sub_type";

        internal Post()
        {
            // ignored
        }

        [JsonProperty(TypeField)]
        internal string PostType { get; set; }

        [JsonProperty("time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset Time { get;  set; }

        [JsonProperty("self_id")]
        public long SelfId { get; set; }
        [JsonProperty("user_id")]
        public long UserId { get; set; }

        public abstract Endpoint Endpoint { get; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Request : Post
    {
        internal const string Friend = "friend";
        internal const string Group = "group";

        internal new const string TypeField = "request_type";

        private readonly Lazy<ReceivedMessage> _commentLazy;
        private readonly Lazy<string> _commentTextLazy;

        internal Request()
        {
            _commentLazy = new Lazy<ReceivedMessage>(() => new ReceivedMessage(ObjComment));
            _commentTextLazy = new Lazy<string>(() => CommentMessage.Text);
        }

        [JsonProperty("comment")]
        private object ObjComment { get; set; }

        [JsonProperty(TypeField)]
        internal string RequestType { get; private set; }
        [JsonProperty("flag")]
        public string Flag { get; private set; }
        public string Comment => _commentTextLazy.Value;

        public ReceivedMessage CommentMessage => _commentLazy.Value;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class FriendRequest : Request
    {
        internal FriendRequest()
        {
            // ignored
        }

        public override Endpoint Endpoint => new PrivateEndpoint(UserId);
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class GroupRequest : Request
    {
        internal const string Add = "add";
        internal const string Invite = "invite";

        internal GroupRequest()
        {
            // ignored
        }

        [JsonProperty(SubTypeField)]
        internal string SubType { get; private set; }

        [JsonProperty("group_id")]
        public long GroupId { get; private set; }

        public override Endpoint Endpoint => new GroupEndpoint(GroupId);
    }
}
