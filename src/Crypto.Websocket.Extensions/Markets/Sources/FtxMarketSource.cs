using System;
using System.Linq;
using System.Reactive.Linq;
using Ftx.Client.Websocket.Client;
using Ftx.Client.Websocket.Responses.Markets;
using Crypto.Websocket.Extensions.Core.Markets.Models;
using Crypto.Websocket.Extensions.Core.Markets.Sources;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using FutureType = Crypto.Websocket.Extensions.Core.Markets.Models.FutureType;
using Group = Crypto.Websocket.Extensions.Core.Markets.Models.Group;
using MarketType = Crypto.Websocket.Extensions.Core.Markets.Models.MarketType;

namespace Crypto.Websocket.Extensions.Markets.Sources
{
    /// <summary>
    /// Ftx markets source
    /// </summary>
    public class FtxMarketSource : MarketSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        private FtxWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public FtxMarketSource(FtxWebsocketClient client)
        {
            ChangeClient(client);
        }

        /// <inheritdoc />
        public override string ExchangeName => "Ftx";

        /// <summary>
        /// Change client and resubscribe to the new streams
        /// </summary>
        public void ChangeClient(FtxWebsocketClient client)
        {
            CryptoValidations.ValidateInput(client, nameof(client));

            _client = client;
            _subscription?.Dispose();
            Subscribe();
        }

        private void Subscribe()
        {
            _subscription = _client.Streams.MarketsStream
                .Where(x => x?.Data != null && x.Data.MarketsData.Any())
                .Subscribe(HandleMarketSafe);
        }

        private void HandleMarketSafe(MarketsResponse response)
        {
            try
            {
                HandleMarket(response);
            }
            catch (Exception e)
            {
                Log.Error(e, $"[Ftx] Failed to handle market info, error: '{e.Message}'");
            }
        }

        private void HandleMarket(MarketsResponse response)
        {
            MarketsSubject.OnNext(ConvertMarkets(response.Data.MarketsData.Values.ToArray()));
        }

        private CryptoMarket[] ConvertMarkets(Market[] markets)
        {
            return markets.Select(ConvertMarket).ToArray();
        }

        private CryptoMarket ConvertMarket(Market market)
        {
            var future = ConvertFuture(market.Future);

            var cryptoMarket = new CryptoMarket
            {
                Name = market.Name,
                Future = future,
                Enabled = market.Enabled,
                PriceIncrement = market.PriceIncrement,
                SizeIncrement = market.SizeIncrement,
                QuoteCurrency = market.QuoteCurrency,
                Underlying = market.Underlying,
                Type = ConvertMarketType(market.Type, market.Name),
                BaseCurrency = market.BaseCurrency
            };

            return cryptoMarket;
        }

        private MarketType ConvertMarketType(Ftx.Client.Websocket.Responses.Markets.MarketType type, string name)
        {
            if (name.EndsWith("perp", StringComparison.InvariantCultureIgnoreCase))
                return MarketType.Perpetual;

            switch (type)
            {
                case Ftx.Client.Websocket.Responses.Markets.MarketType.Future:
                    return MarketType.Future;
                case Ftx.Client.Websocket.Responses.Markets.MarketType.Spot:
                    return MarketType.Spot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private FutureType ConvertFutureType(Ftx.Client.Websocket.Responses.Markets.FutureType type)
        {
            switch (type)
            {
                case Ftx.Client.Websocket.Responses.Markets.FutureType.Future:
                    return FutureType.Future;
                case Ftx.Client.Websocket.Responses.Markets.FutureType.Move:
                    return FutureType.Move;
                case Ftx.Client.Websocket.Responses.Markets.FutureType.Perpetual:
                    return FutureType.Perpetual;
                case Ftx.Client.Websocket.Responses.Markets.FutureType.Prediction:
                    return FutureType.Prediction;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private Group ConvertGroup(Ftx.Client.Websocket.Responses.Markets.Group type)
        {
            switch (type)
            {
                case Ftx.Client.Websocket.Responses.Markets.Group.Daily:
                    return Group.Daily;
                case Ftx.Client.Websocket.Responses.Markets.Group.Perpetual:
                    return Group.Perpetual;
                case Ftx.Client.Websocket.Responses.Markets.Group.Prediction:
                    return Group.Prediction;
                case Ftx.Client.Websocket.Responses.Markets.Group.Quarterly:
                    return Group.Quarterly;
                case Ftx.Client.Websocket.Responses.Markets.Group.Weekly:
                    return Group.Weekly;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private CryptoFuture ConvertFuture(Future future)
        {
            if (future == null) return null;

            var cryptoFuture = new CryptoFuture
            {
                Expiry = future.Expiry,
                Name = future.Name,
                Underlying = future.Underlying,
                Perpetual = future.Perpetual,
                Type = ConvertFutureType(future.Type),
                Group = ConvertGroup(future.Group),
                MoveStart = future.MoveStart
            };

            return cryptoFuture;
        }
    }
}