using System;
using Crypto.Websocket.Extensions.Core.Utils;

namespace Crypto.Websocket.Extensions.Core.Markets.Models
{
    public class CryptoMarket
    {
        /// <summary>
        /// Name to which this position belongs
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Name to which this position belongs (cleaned)
        /// </summary>
        public string PairClean => CryptoPairsHelper.Clean(Name);

        public bool Enabled { get; set; }
        public double? PriceIncrement { get; set; }
        public double? SizeIncrement { get; set; }
        public MarketType Type { get; set; }
        public string BaseCurrency { get; set; }
        public string QuoteCurrency { get; set; }
        public string Underlying { get; set; }
        public CryptoFuture Future { get; set; }
        public double? MakerFee { get; set; }
        public double? TakerFee { get; set; }
    }
}