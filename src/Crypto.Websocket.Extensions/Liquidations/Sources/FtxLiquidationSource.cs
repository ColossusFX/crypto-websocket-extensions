using System;
using System.Linq;
using System.Reactive.Linq;
using Crypto.Websocket.Extensions.Core.Liquidations.Models;
using Crypto.Websocket.Extensions.Core.Liquidations.Sources;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using Ftx.Client.Websocket.Client;
using Ftx.Client.Websocket.Responses;
using Ftx.Client.Websocket.Responses.Trades;

namespace Crypto.Websocket.Extensions.Liquidations.Sources
{
    public class FtxLiquidationSource : LiquidationSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        private FtxWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public FtxLiquidationSource(FtxWebsocketClient client)
        {
            ChangeClient(client);
        }

        /// <inheritdoc />
        public override string ExchangeName => "ftx";

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
            _subscription = _client.Streams.TradesStream
                .Where(x => x != null && x.Data.Any())
                .Select(x => x.Data.Where(y => y.Liquidation))
                .Subscribe(x => HandleLiquidationSafe(x.ToArray()));
        }

        private void HandleLiquidationSafe(Trade[] response)
        {
            try
            {
                HandleLiquidation(response);
            }
            catch (Exception e)
            {
                Log.Error(e, $"[Ftx] Failed to handle trade info, error: '{e.Message}'");
            }
        }

        private void HandleLiquidation(Trade[] trades)
        {
            LiquidationSubject.OnNext(ConvertLiquidations(trades));
        }

        private CryptoLiquidation[] ConvertLiquidations(Trade[] trades)
        {
            return trades.Select(ConvertLiquidation).ToArray();
        }

        private CryptoLiquidation ConvertLiquidation(Trade trade)
        {
            var data = new CryptoLiquidation
            {
                Amount = trade.Size,
                AmountQuote = trade.Size * trade.Price,
                Side = ConvertSide(trade.Side),
                Id = trade.Id.ToString(),
                Price = trade.Price,
                Timestamp = trade.Time.UtcDateTime,
                Pair = trade.Market,
                ExchangeName = ExchangeName,
                Liquidation = trade.Liquidation
            };
            return data;
        }

        private CryptoTradeSide ConvertSide(FtxSide tradeSide)
        {
            if (tradeSide == FtxSide.Undefined)
                return CryptoTradeSide.Undefined;
            return tradeSide == FtxSide.Buy ? CryptoTradeSide.Buy : CryptoTradeSide.Sell;
        }
    }
}