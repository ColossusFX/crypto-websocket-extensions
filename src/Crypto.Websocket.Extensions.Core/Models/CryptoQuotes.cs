﻿using System.Diagnostics;

namespace Crypto.Websocket.Extensions.Core.Models
{
    /// <summary>
    /// Price quotes
    /// </summary>
    [DebuggerDisplay("CryptoQuotes bid: {Bid}/{BidAmount}, ask: {Ask}/{AskAmount}")]
    public class CryptoQuotes : ICryptoQuotes
    {
        /// <summary>
        /// Price quotes
        /// </summary>
        public CryptoQuotes(double bid, double ask, double bidAmount, double askAmount)
        {
            Bid = bid;
            Ask = ask;
            BidAmount = bidAmount;
            AskAmount = askAmount;
            Mid = (bid + ask) / 2;
        }

        /// <summary>
        /// Top level bid price
        /// </summary>
        public double Bid { get; }

        /// <summary>
        /// Top level ask price
        /// </summary>
        public double Ask { get; }

        /// <summary>
        /// Current mid price
        /// </summary>
        public double Mid { get; }

        /// <summary>
        /// Top level bid amount
        /// </summary>
        public double BidAmount { get; }

        /// <summary>
        /// Top level ask amount
        /// </summary>
        public double AskAmount { get; }

        /// <summary>
        /// Returns true if quotes are in valid state
        /// </summary>
        public bool IsValid()
        {
            var isPriceValid = Bid <= Ask;
            return isPriceValid;
        }

        /// <summary>
        /// Format quotes to readable form
        /// </summary>
        public override string ToString()
        {
            return $"bid: {Bid}/{BidAmount}, ask: {Ask}/{AskAmount}";
        }
    }
}
