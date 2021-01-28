using System;
using Crypto.Websocket.Extensions.Core.Markets.Models;

namespace Crypto.Websocket.Extensions.Core.Markets.Sources
{
    public interface IMarketSource
    {
        /// <summary>
        /// Origin exchange name
        /// </summary>
        string ExchangeName { get; }

        /// <summary>
        /// Stream info about current markets
        /// </summary>
        IObservable<CryptoMarket[]> MarketsStream { get; }
    }
}