using System;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.Orders;
using Crypto.Websocket.Extensions.Core.Orders.Models;
using Crypto.Websocket.Extensions.Core.Orders.Sources;
using Crypto.Websocket.Extensions.Core.Utils;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using Ftx.Client.Websocket.Client;
using Ftx.Client.Websocket.Responses.Orders;
using Ftx.Client.Websocket.Responses.Trades;

using OrderStatus = Ftx.Client.Websocket.Responses.Orders.OrderStatus;
using OrderType = Ftx.Client.Websocket.Responses.Orders.OrderType;

namespace Crypto.Websocket.Extensions.Orders.Sources
{
    /// <inheritdoc />
    public class FtxOrderSource: OrderSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();
        private readonly CryptoOrderCollection _partiallyFilledOrders = new CryptoOrderCollection();

        private FtxWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public FtxOrderSource(FtxWebsocketClient client)
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
            _subscription = _client.Streams.OrdersStream.Subscribe(x=>HandleOrdersSafe(x));
        }

        private void HandleOrdersSafe(OrdersResponse response)
        {
            try
            {
                HandleOrder(response);
            }
            catch (Exception e)
            {
                Log.ErrorException($"[Ftx] Failed to handle order info, error: '{response}'", e);
            }
        }

        private void HandleOrder(OrdersResponse response)
        {
            if (response == null)
            {
                // weird state, do nothing
                return;
            }

            var orders = ConvertOrder(response);

            switch (response.Data.Status)
            {
                case OrderStatus.New:
                    OrderCreatedSubject.OnNext(orders);
                    break;
                case OrderStatus.Open: //filled
                case OrderStatus.Closed: //cancelled
                    OrderUpdatedSubject.OnNext(orders);
                    OrderUpdatedSubject.OnNext(orders);
                    break;
                case OrderStatus.Undefined:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private CryptoOrder ConvertOrder(OrdersResponse order)
        {
            var id = order?.Data.Id.ToString() ?? "00000";
            var clientId = order?.Data.ClientId;
            var existingCurrent = ExistingOrders.ContainsKey(id) ? ExistingOrders[id] : null;
            var existingPartial = _partiallyFilledOrders.ContainsKey(id) ? _partiallyFilledOrders[id] : null;
            var existing = existingPartial ?? existingCurrent;

            var price = Math.Abs(FirstNonZero(order?.Data.Price, existing?.Price) ?? 0);

            var amount = Math.Abs(FirstNonZero(order?.Data.Size, existing?.AmountOrig) ?? 0);

            var amountOrig = Math.Abs(order?.Data.Size ?? 0);

            var currentStatus = existing != null &&
                                existing.OrderStatus != CryptoOrderStatus.Undefined &&
                                existing.OrderStatus != CryptoOrderStatus.New &&
                                order?.Data.Status == OrderStatus.Undefined
                ? existing.OrderStatus
                : ConvertOrderStatus(order);

            var newOrder = new CryptoOrder
            {
                Id = id,
                Pair = CryptoPairsHelper.Clean(order?.Data.Market),
                Price = price,
                AmountFilled = order?.Data.FilledSize,
                AmountOrig = amountOrig,
                Side = ConvertSide(order?.Data.Side?? TradeSide.Undefined),
                OrderStatus = ConvertOrderStatus(order),
                Type = ConvertOrderType(order?.Data.Type ?? OrderType.Undefined),
                Created = ConvertToDatetime(order?.Data.CreatedAt ?? DateTimeOffset.UtcNow),
                ClientId = clientId
            };

            if (currentStatus == CryptoOrderStatus.PartiallyFilled)
            {
                // save partially filled orders
                _partiallyFilledOrders[newOrder.Id] = newOrder;
            }

            return newOrder;
        }

        private CryptoOrderType ConvertOrderType(OrderType orderType)
        {
            switch (orderType)
            {
                case OrderType.Undefined:
                    break;
                case OrderType.Limit:
                    return CryptoOrderType.Limit;
                case OrderType.Market:
                    return CryptoOrderType.Market;
                case OrderType.Stop:
                    break;
                case OrderType.TrailingStop:
                    break;
                case OrderType.Fok:
                    break;
                case OrderType.StopLimit:
                    return CryptoOrderType.StopLimit;
                    break;
                case OrderType.TakeProfitLimit:
                    break;
                case OrderType.TakeProfitMarket:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(orderType), orderType, null);
            }

            return CryptoOrderType.Undefined;
        }
        
        private DateTime ConvertToDatetime(DateTimeOffset dateTimeOffset)
        {
            var sourceTime = new DateTimeOffset(dateTimeOffset.DateTime, TimeSpan.Zero);
            return sourceTime.DateTime;
        }
        private CryptoOrderStatus ConvertOrderStatus(OrdersResponse order)
        {
            switch (order.Data.Status)
            {
                case OrderStatus.New:
                    break;
                case OrderStatus.Open: //filled
                    break;
                case OrderStatus.Closed: //cancelled
                    break;
                case OrderStatus.Undefined:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return CryptoOrderStatus.Undefined;
        }
        
        private CryptoOrderSide ConvertSide(TradeSide side)
        {
            if (side == TradeSide.Buy) return CryptoOrderSide.Bid;

            if (side == TradeSide.Sell) return CryptoOrderSide.Ask;

            return CryptoOrderSide.Undefined;
        }

        private static double? FirstNonZero(params double?[] numbers)
        {
            foreach (var number in numbers)
                if (number.HasValue && Math.Abs(number.Value) > 0)
                    return number.Value;

            return null;
        }

        private static double? Abs(double? value)
        {
            if (!value.HasValue) return null;

            return Math.Abs(value.Value);
        }
    }
}