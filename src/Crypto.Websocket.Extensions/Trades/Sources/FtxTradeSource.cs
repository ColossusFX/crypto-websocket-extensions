using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.Trades.Models;
using Crypto.Websocket.Extensions.Core.Trades.Sources;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using Ftx.Client.Websocket.Client;
using Ftx.Client.Websocket.Responses;
using Ftx.Client.Websocket.Responses.Trades;

namespace Crypto.Websocket.Extensions.Trades.Sources
{
    public class FtxTradeSource : TradeSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        private FtxWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public FtxTradeSource(FtxWebsocketClient client)
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
            _subscription = _client.Streams.TradesStream
                .Where(x => x != null)
                .Subscribe(HandleTradeSafe);
        }

        private void HandleTradeSafe(TradeResponse response)
        {
            try
            {
                HandleTrade(response);
            }
            catch (Exception e)
            {
                Log.Error(e, $"[Ftx] Failed to handle trade info, error: '{e.Message}'");
            }
        }

        private void HandleTrade(TradeResponse response)
        {
            TradesSubject.OnNext(ConvertTrades(response.Market, response.Data));
        }

        private CryptoTrade[] ConvertTrades(string market, Trade[] trades)
        {
            var list = new List<CryptoTrade>();
            foreach (var trade in trades)
            {
                list.Add(ConvertTrade(market, trade));
            }

            //return trades.Select(ConvertTrade(market)).ToArray();
            return list.ToArray();
        }

        private CryptoTrade ConvertTrade(string symbol, Trade trade)
        {
            var data = new CryptoTrade()
            {
                Amount = trade.Size,
                AmountQuote = trade.Size * trade.Price,
                Side = ConvertSide(trade.Side),
                Id = trade.Id.ToString(),
                Price = trade.Price,
                Timestamp = trade.Time.UtcDateTime,
                Pair = symbol,
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