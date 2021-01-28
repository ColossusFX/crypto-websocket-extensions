using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Crypto.Websocket.Extensions.Core.Liquidations.Models;

namespace Crypto.Websocket.Extensions.Core.Liquidations.Sources
{
    /// <summary>
    /// Source that provides info about executed trades 
    /// </summary>
    public abstract class LiquidationSourceBase : ILiquidationSource
    {
        /// <summary>
        /// Trades subject
        /// </summary>
        protected readonly Subject<CryptoLiquidation[]> LiquidationSubject = new Subject<CryptoLiquidation[]>();


        /// <summary>
        /// Origin exchange name
        /// </summary>
        public abstract string ExchangeName { get; }

        /// <summary>
        /// Stream info about executed trades
        /// </summary>
        public virtual IObservable<CryptoLiquidation[]> LiquidationStream => LiquidationSubject.AsObservable();
    }
}
