using System;
using System.Diagnostics;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.Utils;

namespace Crypto.Websocket.Extensions.Core.Liquidations.Models
{
    /// <summary>
    /// Executed trade info
    /// </summary>
    [DebuggerDisplay("Trade: {Id} - {Pair} - {Price} {Amount}/{AmountQuote}")]
    public class CryptoLiquidation : CryptoChangeInfo
    {
        private CryptoTradeSide _side;
        private double _amount;
        private double _amountQuote;

        /// <summary>
        /// Unique trade id (provided by exchange)
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Trade is liquidation (FTX)
        /// </summary>
        public bool Liquidation { get; set; }

        /// <summary>
        /// Unique related order id from maker side - liquidity provider (provided only by few exchanges)
        /// </summary>
        public string MakerOrderId { get; set; }

        /// <summary>
        /// Unique related order id from taker side - liquidity taker (provided only by few exchanges)
        /// </summary>
        public string TakerOrderId { get; set; }

        /// <summary>
        /// Name to which this trade belongs
        /// </summary>
        public string Pair { get; set; }

        /// <summary>
        /// Name to which this trade belongs (cleaned)
        /// </summary>
        public string PairClean => CryptoPairsHelper.Clean(Pair);

        /// <summary>
        /// Trade's executed timestamp
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Trade's price
        /// </summary>
        public double Price { get; set; }

        /// <summary>
        /// Original trade amount (stable) in base currency
        /// </summary>
        public double Amount
        {
            get => (_amount);
            set => _amount = WithCorrectSign(value);
        }

        /// <summary>
        /// Original trade amount (stable) in quote currency
        /// </summary>
        public double AmountQuote
        {
            get => (_amountQuote);
            set => _amountQuote = WithCorrectSign(value);
        }

        /// <summary>
        /// Trade's side
        /// </summary>
        public CryptoTradeSide Side
        {
            get => _side;
            set
            {
                _side = value;

                _amount = WithCorrectSign(_amount);
                _amountQuote = WithCorrectSign(_amountQuote);
            }
        }

        private double WithCorrectSign(double value)
        {
            if (_side == CryptoTradeSide.Undefined)
                return value;
            return Math.Abs(value) * (_side == CryptoTradeSide.Buy ? 1 : -1);
        }


    }
}
