using System;
using Newtonsoft.Json;

namespace Crypto.Websocket.Extensions.Core.Markets.Models
{
    public class CryptoFuture
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("underlying")]
        public string Underlying { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("type")]
        public FutureType Type { get; set; }

        [JsonProperty("expiry")]
        public DateTimeOffset? Expiry { get; set; }

        [JsonProperty("perpetual")]
        public bool Perpetual { get; set; }

        [JsonProperty("expired")]
        public bool Expired { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("postOnly")]
        public bool PostOnly { get; set; }

        [JsonProperty("imfFactor")]
        public double ImfFactor { get; set; }

        [JsonProperty("underlyingDescription")]
        public string UnderlyingDescription { get; set; }

        [JsonProperty("expiryDescription")]
        public string ExpiryDescription { get; set; }

        [JsonProperty("moveStart")]
        public DateTimeOffset? MoveStart { get; set; }

        [JsonProperty("positionLimitWeight")]
        public double PositionLimitWeight { get; set; }

        [JsonProperty("group")]
        public Group Group { get; set; }
    }
}