using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Crypto.Websocket.Extensions.Core.Markets.Models;

namespace Crypto.Websocket.Extensions.Core.Markets.Sources
{
    public abstract class MarketSourceBase : IMarketSource
    {
        /// <summary>
        /// Market subject
        /// </summary>
        protected readonly Subject<CryptoMarket[]> MarketsSubject = new Subject<CryptoMarket[]>();


        /// <summary>
        /// Origin exchange name
        /// </summary>
        public abstract string ExchangeName { get; }

        /// <summary>
        /// Stream info about markets
        /// </summary>
        public virtual IObservable<CryptoMarket[]> MarketsStream => MarketsSubject.AsObservable();

    }
}