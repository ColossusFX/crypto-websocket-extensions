using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Crypto.Websocket.Extensions.Core.Models;
using Crypto.Websocket.Extensions.Core.OrderBooks;
using Crypto.Websocket.Extensions.Core.OrderBooks.Models;
using Crypto.Websocket.Extensions.Core.OrderBooks.Sources;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using Ftx.Client.Websocket.Client;
using Ftx.Client.Websocket.Responses;
using Ftx.Client.Websocket.Responses.Books;
using OrderBookLevel = Crypto.Websocket.Extensions.Core.OrderBooks.Models.OrderBookLevel;
using FtxOrderBookLevel = Ftx.Client.Websocket.Responses.Books.OrderBookLevel;

namespace Crypto.Websocket.Extensions.OrderBooks.Sources
{
    /// <inheritdoc />
    public class FtxOrderBookSource : OrderBookSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();
        private FtxWebsocketClient _client;
        private IDisposable _subscription;
        private IDisposable _subscriptionSnapshot;


        /// <inheritdoc />
        public FtxOrderBookSource(FtxWebsocketClient client)
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
            _subscriptionSnapshot?.Dispose();
            _subscription?.Dispose();
            Subscribe();
        }

        private void Subscribe()
        {
            _subscriptionSnapshot = _client.Streams.OrderBookSnapshotStream.Subscribe(HandleSnapshot);
            _subscription = _client.Streams.OrderBookUpdateStream.Subscribe(HandleBook);
        }

        private void HandleSnapshot(OrderBookSnapshotResponse snapshot)
        {
            // received snapshot, convert and stream
            var levels = ConvertSnapshot(snapshot);
            var bulk = new OrderBookLevelBulk(OrderBookAction.Insert, levels, CryptoOrderBookType.L2);
            FillBulk(snapshot, bulk);
            StreamSnapshot(bulk);
        }

        private OrderBookLevel[] ConvertSnapshot(OrderBookSnapshotResponse snapshot)
        {
            var bids = ConvertLevels(snapshot.Market, snapshot.Data.Bids);
            var asks = ConvertLevels(snapshot.Market, snapshot.Data.Asks);
            var levels = bids.Concat(asks).ToArray();
            return levels;
        }

        private void HandleBook(OrderBookUpdateResponse update)
        {
            BufferData(update);
        }

        private OrderBookLevel[] ConvertLevels(string pair, FtxOrderBookLevel[] data)
        {
            return data
                .Select(x => ConvertLevel(pair, x))
                .ToArray();
        }

        private OrderBookLevel ConvertLevel(string pair, FtxOrderBookLevel x)
        {
            return new OrderBookLevel
            (
                x.Price.ToString(CultureInfo.InvariantCulture),
                ConvertSide(x.Side),
                x.Price,
                x.Amount,
                null,
                pair
            );
        }

        private CryptoOrderSide ConvertSide(OrderBookSide side)
        {
            if (side == OrderBookSide.Buy) return CryptoOrderSide.Bid;

            if (side == OrderBookSide.Sell) return CryptoOrderSide.Ask;

            return CryptoOrderSide.Undefined;
        }

        private OrderBookAction RecognizeAction(OrderBookLevel level)
        {
            if (level.Amount > 0) return OrderBookAction.Update;

            return OrderBookAction.Delete;
        }

        /// <inheritdoc />
        protected override async Task<OrderBookLevelBulk> LoadSnapshotInternal(string pair, int count)
        {
            return null;
        }

        private IEnumerable<OrderBookLevelBulk> ConvertDiff(OrderBookUpdateResponse update)
        {
            var convertedBids = ConvertLevels(update.Market, update.Data.Asks);
            var convertedAsks = ConvertLevels(update.Market, update.Data.Bids);

            var converted = convertedAsks.Concat(convertedBids);

            var group = converted.GroupBy(RecognizeAction).ToArray();

            foreach (var actionGroup in group)
            {
                var bulk = new OrderBookLevelBulk(actionGroup.Key, actionGroup.ToArray(), CryptoOrderBookType.L2);
                FillBulk(update, bulk);
                yield return bulk;
            }
        }

        private void FillBulk(ResponseBase response, OrderBookLevelBulk bulk)
        {
            if (response == null)
                return;

            bulk.ExchangeName = ExchangeName;
        }

        
        /// <inheritdoc />
        protected override OrderBookLevelBulk[] ConvertData(object[] data)
        {
            var result = new List<OrderBookLevelBulk>();
            foreach (var response in data)
            {
                var responseSafe = response as OrderBookUpdateResponse;
                if(responseSafe == null)
                    continue;

                var converted = ConvertDiff(responseSafe);
                result.AddRange(converted);
            }

            return result.ToArray();
        }
    }
}