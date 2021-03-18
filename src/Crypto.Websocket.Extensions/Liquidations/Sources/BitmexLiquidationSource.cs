using System;
using System.Linq;
using System.Reactive.Linq;
using Crypto.Websocket.Extensions.Core.Liquidations.Models;
using Crypto.Websocket.Extensions.Core.Liquidations.Sources;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using Bitmex.Client.Websocket.Client;
using Bitmex.Client.Websocket.Responses;
using Bitmex.Client.Websocket.Responses.Liquidation;
using Bitmex.Client.Websocket.Responses.Trades;
using Crypto.Websocket.Extensions.Core.Liquidations;

namespace Crypto.Websocket.Extensions.Liquidations.Sources
{
    public class BitmexLiquidationSource : LiquidationSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        private BitmexWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public BitmexLiquidationSource(BitmexWebsocketClient client)
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
            _subscription = _client.Streams.LiquidationStream
                .Where(x => x != null && x.Data.Any())
                .Subscribe(x => HandleLiquidationSafe(x.Action, x.Data));
        }


        private void HandleLiquidationSafe(BitmexAction action, Liquidation[] trades)
        {
            try
            {
                HandleLiquidation(action, trades);
            }
            catch (Exception e)
            {
                Log.Error(e, $"[Bitmex] Failed to handle trade info, error: '{e.Message}'");
            }
        }

        private void HandleLiquidation(BitmexAction action, Liquidation[] trades)
        {
            LiquidationSubject.OnNext(ConvertLiquidation(action, trades));
        }

        private CryptoLiquidation[] ConvertLiquidation(BitmexAction action, Liquidation[] data)
        {
            return data
                .Select(x => ConvertLiquidation(action, x))
                .ToArray();
        }

        private CryptoLiquidation ConvertLiquidation(BitmexAction action, Liquidation trade)
        {
            return new CryptoLiquidation()
            {
                ExchangeName = "bitmex",
                Amount = Convert.ToDouble(trade.leavesQty ?? 0),
                AmountQuote = Convert.ToDouble(trade.leavesQty ?? 0 * trade.Price ?? 0),
                Side = ConvertTradeSide(trade.Side ?? BitmexSide.Undefined),
                Id = trade.OrderID,
                Price = Convert.ToDouble(trade.Price ?? 0),
                Timestamp = DateTime.Now,
                Pair = trade.Symbol,
                Liquidation = true,
                Action = ConvertAction(action)
            };
        }

        private CryptoAction ConvertAction(BitmexAction side)
        {
            switch (side)
            {
                case BitmexAction.Undefined:
                    break;
                case BitmexAction.Partial:
                    return CryptoAction.Partial;
                case BitmexAction.Insert:
                    return CryptoAction.Insert;
                case BitmexAction.Update:
                    return CryptoAction.Update;
                case BitmexAction.Delete:
                    return CryptoAction.Delete;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }

            return CryptoAction.Undefined;
        }

        private CryptoTradeSide ConvertTradeSide(BitmexSide? side)
        {
            switch (side)
            {
                case BitmexSide.Undefined:
                    break;
                case BitmexSide.Buy:
                    return CryptoTradeSide.Buy;
                case BitmexSide.Sell:
                    return CryptoTradeSide.Sell;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, $"error {side}");
            }

            return CryptoTradeSide.Undefined;
        }

        /*private CryptoAction ConvertAction(BitmexAction action)
        {
            switch (action)
            {
                case BitmexAction.Undefined:
                    break;
                case BitmexAction.Partial:
                    return CryptoAction.Partial;
                case BitmexAction.Insert:
                    return CryptoAction.Insert;
                case BitmexAction.Update:
                    return CryptoAction.Update;
                case BitmexAction.Delete:
                    return CryptoAction.Delete;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }

            return CryptoAction.Partial;
        }*/
    }
}