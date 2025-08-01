using System;

namespace Sisters.WudiLib
{
    public class RawMessage : Message
    {
        private readonly string _raw;

        public RawMessage(string raw) => _raw = raw ?? throw new ArgumentNullException(nameof(raw));

        public override string Raw => _raw;

        protected internal override object Serializing => _raw;
        
        public override string LoggableRaw => _raw.Substring(0, 100);

        public static RawMessage operator +(RawMessage left, RawMessage right)
            => new RawMessage(left._raw + right._raw);
    }
}
