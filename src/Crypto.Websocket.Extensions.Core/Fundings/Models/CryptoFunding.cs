using System;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.Utils;

namespace Crypto.Websocket.Extensions.Core.Fundings
{
    public class CryptoFunding : CryptoChangeInfo
    {
        /// <summary>
        /// Name to which this trade belongs
        /// </summary>
        public string Pair { get; set; }
        
        /// <summary>
        /// Funding rate for the period (time)
        /// </summary>
        public double Rate { get; set; }

        public double RatePercent => Rate * 100;
        
        public double RatePercentDaily => Rate * 100 * 24;
        
        public double RateApy => Rate * 100 * 24 * 365;
        
        /// <summary>
        /// Indicative funding rate
        /// </summary>
        public double IndicativeRate { get; set; }
        
        /// <summary>
        /// Indicative funding rate %
        /// </summary>
        public double IndicativeRatePercent => IndicativeRate * 100;
        
        /// <summary>
        /// Funding period
        /// </summary>
        public DateTime Time { get; set; }
        
        public DateTime Interval { get; set; }

        /// <summary>
        /// Name to which this trade belongs (cleaned)
        /// </summary>
        public string PairClean => CryptoPairsHelper.Clean(Pair);

        public double PredictedExpirationPrice { get; set; }
        public double StrikePrice { get; set; }
        public double OpenInterest { get; set; }
    }
}