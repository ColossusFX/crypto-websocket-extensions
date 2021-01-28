using System;
using Crypto.Websocket.Extensions.Core.Liquidations.Models;

namespace Crypto.Websocket.Extensions.Core.Liquidations.Sources
{
    /// <summary>
    /// Source that provides info about executed trades 
    /// </summary>
    public interface ILiquidationSource
    {
        /// <summary>
        /// Origin exchange name
        /// </summary>
        string ExchangeName { get; }

        /// <summary>
        /// Stream info about executed trades
        /// </summary>
        IObservable<CryptoLiquidation[]> LiquidationStream { get; }
    }
}
