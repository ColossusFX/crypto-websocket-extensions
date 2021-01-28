using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using Bitmex.Client.Websocket.Client;
using Bitmex.Client.Websocket.Responses.Instruments;
using Crypto.Websocket.Extensions.Core.Markets.Models;
using Crypto.Websocket.Extensions.Core.Markets.Sources;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;

namespace Crypto.Websocket.Extensions.Markets.Sources
{
    /// <summary>
    /// Bitmex markets source
    /// </summary>
    public class BitmexMarketSource : MarketSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        private BitmexWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public BitmexMarketSource(BitmexWebsocketClient client)
        {
            ChangeClient(client);
        }

        /// <inheritdoc />
        public override string ExchangeName => "bitmex";

        /// <summary>
        /// Change client and resubscribe to the new streams
        /// </summary>
        public void ChangeClient(BitmexWebsocketClient client)
        {
            CryptoValidations.ValidateInput(client, nameof(client));

            _client = client;
            _subscription?.Dispose();
            Subscribe();
        }

        private void Subscribe()
        {
            _subscription = _client.Streams.InstrumentStream
                .Where(x => x?.Data != null && x.Data.Any())
                .Subscribe(HandleMarketSafe);
        }

        private void HandleMarketSafe(InstrumentResponse response)
        {
            try
            {
                HandleMarket(response);
            }
            catch (Exception e)
            {
                Log.Error(e, $"[Bitmex] Failed to handle market info, error: '{e.Message}'");
            }
        }

        private void HandleMarket(InstrumentResponse response)
        {
            MarketsSubject.OnNext(ConvertMarkets(response.Data));
        }

        private CryptoMarket[] ConvertMarkets(Instrument[] trades)
        {
            return trades.Select(ConvertMarket).ToArray();
        }

        private CryptoMarket ConvertMarket(Instrument instrument)
        {
            var future = ConvertFuture(instrument);

            MarketType marketType = MarketType.Future;
            if (future == null)
                marketType = MarketType.Perpetual;
            if (future != null)
                marketType = MarketType.Future;

            var market = new CryptoMarket
            {
                Name = instrument.Symbol,
                Future = future,
                PriceIncrement = instrument.TickSize,
                QuoteCurrency = instrument.QuoteCurrency,
                Underlying = instrument.Underlying,
                Type = marketType,
                BaseCurrency = instrument.RootSymbol,
                MakerFee = instrument.MakerFee,
                TakerFee = instrument.TakerFee
            };

            return market;
        }

        private CryptoFuture ConvertFuture(Instrument instrument)
        {
            var isFutures = IsFuturesMarket(instrument.Symbol);

            if (!isFutures) return null;

            var future = new CryptoFuture
            {
                Expiry = instrument.Expiry,
                Name = instrument.Symbol,
                Underlying = instrument.Underlying,
                Type = FutureType.Future
            };
            return future;
        }

        private bool IsFuturesMarket(string symbol)
        {
            var endStr = GetLast(symbol, 2);

            return int.TryParse(endStr, out var number);
        }

        private string GetLast(string source, int endlength)
        {
            if (endlength >= source.Length)
                return source;
            return source.Substring(source.Length - endlength);
        }
    }
}