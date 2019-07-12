﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crypto.Websocket.Extensions.Models;
using Crypto.Websocket.Extensions.OrderBooks;
using Crypto.Websocket.Extensions.OrderBooks.Models;
using Crypto.Websocket.Extensions.OrderBooks.Sources;
using Crypto.Websocket.Extensions.Utils;
using Xunit;

namespace Crypto.Websocket.Extensions.Tests
{
    public class CryptoOrderBookTests
    {
        [Fact]
        public void StreamingSnapshot_ShouldHandleCorrectly()
        {
            var pair = "BTC/USD";
            var data = GetOrderBookSnapshotMockData(pair, 500);
            var source = new OrderBookSourceMock(data);

            var orderBook = new CryptoOrderBook(pair, source);

            source.StreamSnapshot();

            Assert.Equal(500, orderBook.BidLevels.Length);
            Assert.Equal(500, orderBook.AskLevels.Length);

            Assert.Equal(499, orderBook.BidPrice);
            Assert.Equal(1499, orderBook.BidLevels.First().Amount);

            Assert.Equal(501, orderBook.AskPrice);
            Assert.Equal(2501, orderBook.AskLevels.First().Amount);

            var levels = orderBook.Levels;
            foreach (var level in levels)
            {
                Assert.Equal(CryptoPairsHelper.Clean(pair), level.Pair);
            }
        }

        [Fact]
        public void StreamingSnapshot_DifferentPairs_ShouldHandleCorrectly()
        {
            var pair1 = "BTC/USD";
            var pair2 = "ETH/BTC";
            var data1 = GetOrderBookSnapshotMockData(pair1, 500);
            var data2 = GetOrderBookSnapshotMockData(pair2, 200);
            var data = data2.Concat(data1).ToArray();
            var source = new OrderBookSourceMock(data);

            var orderBook1 = new CryptoOrderBook(pair1, source);
            var orderBook2 = new CryptoOrderBook(pair2, source);

            source.StreamSnapshot();

            Assert.Equal(500, orderBook1.BidLevels.Length);
            Assert.Equal(500, orderBook1.AskLevels.Length);

            Assert.Equal(200, orderBook2.BidLevels.Length);
            Assert.Equal(200, orderBook2.AskLevels.Length);

            Assert.Equal(499, orderBook1.BidLevels.First().Price);
            Assert.Equal(1499, orderBook1.BidLevels.First().Amount);

            Assert.Equal(199, orderBook2.BidLevels.First().Price);
            Assert.Equal(599, orderBook2.BidLevels.First().Amount);

            Assert.Equal(501, orderBook1.AskLevels.First().Price);
            Assert.Equal(2501, orderBook1.AskLevels.First().Amount);

            Assert.Equal(201, orderBook2.AskLevels.First().Price);
            Assert.Equal(1001, orderBook2.AskLevels.First().Amount);
            
            var levels = orderBook1.Levels;
            foreach (var level in levels)
            {
                Assert.Equal(CryptoPairsHelper.Clean(pair1), level.Pair);
            }
        }

        [Fact]
        public void FindLevel_ShouldReturnCorrectValue()
        {
            var pair1 = "BTC/USD";
            var pair2 = "ETH/BTC";
            var data1 = GetOrderBookSnapshotMockData(pair1, 500);
            var data2 = GetOrderBookSnapshotMockData(pair2, 200);
            var data = data2.Concat(data1).ToArray();
            var source = new OrderBookSourceMock(data);

            var orderBook = new CryptoOrderBook(pair1, source);

            source.StreamSnapshot();

            Assert.Equal(1000, orderBook.FindBidLevelByPrice(0)?.Amount);
            Assert.Equal(1000, orderBook.FindBidLevelById("0-bid")?.Amount);

            Assert.Equal(3000, orderBook.FindAskLevelByPrice(1000)?.Amount);
            Assert.Equal(3000, orderBook.FindAskLevelById("1000-ask")?.Amount);
        }

        [Fact]
        public async Task StreamingDiff_BeforeSnapshot_ShouldDoNothing()
        {
            var pair = "BTC/USD";
            var source = new OrderBookSourceMock();
            var orderBook = new CryptoOrderBook(pair, source);

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair, 100, 50, CryptoSide.Bid),
                CreateLevel(pair, 55, 600, CryptoSide.Bid),
                CreateLevel(pair, 105, 400, CryptoSide.Ask),
                CreateLevel(pair, 200, 3000, CryptoSide.Ask)
            ));

            await Task.Delay(100);

            Assert.Empty(orderBook.BidLevels);
            Assert.Empty(orderBook.AskLevels);

            Assert.Equal(0, orderBook.BidPrice);
            Assert.Equal(0, orderBook.AskPrice);
        }

        [Fact]
        public async Task StreamingDiff_ShouldHandleCorrectly()
        {
            var pair = "BTC/USD";
            var data = GetOrderBookSnapshotMockData(pair, 500);
            var source = new OrderBookSourceMock(data);

            var orderBook = new CryptoOrderBook(pair, source);

            source.StreamSnapshot();

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair, 499.4, 50, CryptoSide.Bid),
                CreateLevel(pair, 498.3, 600, CryptoSide.Bid),
                CreateLevel(pair, 300.33, 3350, CryptoSide.Bid),
                CreateLevel(pair, 500.2, 400, CryptoSide.Ask),
                CreateLevel(pair, 503.1, 3000, CryptoSide.Ask),
                CreateLevel(pair, 800.123, 1234, CryptoSide.Ask),

                CreateLevel(null, 101.1, null, CryptoSide.Bid),
                CreateLevel(null, 901.1, null, CryptoSide.Ask)
            ));

            source.StreamBulk(GetUpdateBulk(
                CreateLevel(pair, 499, 33, CryptoSide.Bid),
                CreateLevel(pair, 450, 33, CryptoSide.Bid),
                CreateLevel(pair, 501, 32, CryptoSide.Ask),
                CreateLevel(pair, 503.1, 32, CryptoSide.Ask),

                CreateLevel(pair, 100, null, CryptoSide.Bid),
                CreateLevel(pair, 900, null, CryptoSide.Ask)
            ));

            source.StreamBulk(GetDeleteBulk(
                CreateLevel(pair, 0, CryptoSide.Bid),
                CreateLevel(pair, 1, CryptoSide.Bid),
                CreateLevel(pair, 1000, CryptoSide.Ask),
                CreateLevel(pair, 999, CryptoSide.Ask)
            ));

            await Task.Delay(100);

            Assert.NotEmpty(orderBook.BidLevels);
            Assert.NotEmpty(orderBook.AskLevels);

            Assert.Equal(501, orderBook.BidLevels.Length);
            Assert.Equal(501, orderBook.AskLevels.Length);

            Assert.Equal(499.4, orderBook.BidPrice);
            Assert.Equal(500.2, orderBook.AskPrice);

            Assert.Equal(33, orderBook.FindBidLevelByPrice(499)?.Amount);
            Assert.Equal(33, orderBook.FindBidLevelByPrice(450)?.Amount);

            Assert.Equal(32, orderBook.FindAskLevelByPrice(501)?.Amount);
            Assert.Equal(32, orderBook.FindAskLevelByPrice(503.1)?.Amount);

            var notCompleteBid = orderBook.FindBidLevelByPrice(100);
            Assert.Equal(CryptoPairsHelper.Clean(pair), notCompleteBid.Pair);
            Assert.Equal(1100, notCompleteBid.Amount);
            Assert.Equal(3, notCompleteBid.Count);

            var notCompleteAsk = orderBook.FindAskLevelByPrice(900);
            Assert.Equal(CryptoPairsHelper.Clean(pair), notCompleteAsk.Pair);
            Assert.Equal(2900, notCompleteAsk.Amount);
            Assert.Equal(3, notCompleteAsk.Count);

            Assert.Null(orderBook.FindBidLevelByPrice(0));
            Assert.Null(orderBook.FindBidLevelByPrice(1));
            Assert.Null(orderBook.FindAskLevelByPrice(1000));
            Assert.Null(orderBook.FindAskLevelByPrice(999));

            Assert.Null(orderBook.FindBidLevelByPrice(101.1));
            Assert.Null(orderBook.FindAskLevelByPrice(901.1));
        }

        [Fact]
        public async Task StreamingDiff_TwoPairs_ShouldHandleCorrectly()
        {
            var pair1 = "BTC/USD";
            var pair2 = "ETH/USD";

            var data1 = GetOrderBookSnapshotMockData(pair1, 500);
            var data2 = GetOrderBookSnapshotMockData(pair2, 200);
            var data = data2.Concat(data1).ToArray();
            var source = new OrderBookSourceMock(data);

            var orderBook1 = new CryptoOrderBook(pair1, source) {DebugEnabled = true};
            var orderBook2 = new CryptoOrderBook(pair2, source) {DebugEnabled = true};

            source.StreamSnapshot();

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair2, 199.4, 50, CryptoSide.Bid),
                CreateLevel(pair2, 198.3, 600, CryptoSide.Bid),
                CreateLevel(pair2, 50.33, 3350, CryptoSide.Bid),

                CreateLevel(pair1, 500.2, 400, CryptoSide.Ask),
                CreateLevel(pair1, 503.1, 3000, CryptoSide.Ask),
                CreateLevel(pair1, 800.123, 1234, CryptoSide.Ask),

                CreateLevel(null, 101.1, null, CryptoSide.Bid),
                CreateLevel(null, 901.1, null, CryptoSide.Ask)
            ));

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair1, 499.4, 50, CryptoSide.Bid),
                CreateLevel(pair1, 498.3, 600, CryptoSide.Bid),
                CreateLevel(pair1, 300.33, 3350, CryptoSide.Bid),

                CreateLevel(pair2, 200.2, 400, CryptoSide.Ask),
                CreateLevel(pair2, 203.1, 3000, CryptoSide.Ask),
                CreateLevel(pair2, 250.123, 1234, CryptoSide.Ask)
            ));

            source.StreamBulk(GetUpdateBulk(
                CreateLevel(pair1, 499, 33, CryptoSide.Bid),
                CreateLevel(pair1, 450, 33, CryptoSide.Bid),
                CreateLevel(pair1, 501, 32, CryptoSide.Ask),
                CreateLevel(pair1, 503.1, 32, CryptoSide.Ask),

                CreateLevel(pair1, 100, null, CryptoSide.Bid),
                CreateLevel(pair1, 900, null, CryptoSide.Ask)
            ));

            source.StreamBulk(GetDeleteBulk(
                CreateLevel(pair1, 0, CryptoSide.Bid),
                CreateLevel(pair1, 1, CryptoSide.Bid),

                CreateLevel(pair2, 0, CryptoSide.Bid),
                CreateLevel(pair2, 1, CryptoSide.Bid)
            ));

            source.StreamBulk(GetDeleteBulk(
                CreateLevel(pair2, 400, CryptoSide.Ask),
                CreateLevel(pair2, 399, CryptoSide.Ask),

                CreateLevel(pair1, 1000, CryptoSide.Ask),
                CreateLevel(pair1, 999, CryptoSide.Ask)
            ));

            await Task.Delay(100);

            Assert.NotEmpty(orderBook1.BidLevels);
            Assert.NotEmpty(orderBook1.AskLevels);

            Assert.Equal(501, orderBook1.BidLevels.Length);
            Assert.Equal(501, orderBook1.AskLevels.Length);

            Assert.Equal(201, orderBook2.BidLevels.Length);
            Assert.Equal(201, orderBook2.AskLevels.Length);

            Assert.Equal(499.4, orderBook1.BidPrice);
            Assert.Equal(500.2, orderBook1.AskPrice);

            Assert.Equal(199.4, orderBook2.BidPrice);
            Assert.Equal(200.2, orderBook2.AskPrice);

            Assert.Equal(33, orderBook1.FindBidLevelByPrice(499)?.Amount);
            Assert.Equal(33, orderBook1.FindBidLevelByPrice(450)?.Amount);

            Assert.Equal(32, orderBook1.FindAskLevelByPrice(501)?.Amount);
            Assert.Equal(32, orderBook1.FindAskLevelByPrice(503.1)?.Amount);

            var notCompleteBid = orderBook1.FindBidLevelByPrice(100);
            Assert.Equal(CryptoPairsHelper.Clean(pair1), notCompleteBid.Pair);
            Assert.Equal(1100, notCompleteBid.Amount);
            Assert.Equal(3, notCompleteBid.Count);

            var notCompleteAsk = orderBook1.FindAskLevelByPrice(900);
            Assert.Equal(CryptoPairsHelper.Clean(pair1), notCompleteAsk.Pair);
            Assert.Equal(2900, notCompleteAsk.Amount);
            Assert.Equal(3, notCompleteAsk.Count);

            Assert.Null(orderBook1.FindBidLevelByPrice(0));
            Assert.Null(orderBook1.FindBidLevelByPrice(1));
            Assert.Null(orderBook1.FindAskLevelByPrice(1000));
            Assert.Null(orderBook1.FindAskLevelByPrice(999));

            Assert.Null(orderBook1.FindBidLevelByPrice(101.1));
            Assert.Null(orderBook1.FindAskLevelByPrice(901.1));

            Assert.Null(orderBook2.FindBidLevelByPrice(101.1));
            Assert.Null(orderBook2.FindAskLevelByPrice(901.1));
        }

        [Fact]
        public async Task StreamingData_ShouldNotifyCorrectly()
        {
            var pair = "BTC/USD";
            var data = GetOrderBookSnapshotMockData(pair, 500);
            var source = new OrderBookSourceMock(data);

            var notificationCount = 0;
            var notificationBidAskCount = 0;
            var notificationTopLevelCount = 0;

            var changes = new List<OrderBookChangeInfo>();

            var orderBook = new CryptoOrderBook(pair, source) {DebugEnabled = true};

            orderBook.OrderBookUpdatedStream.Subscribe(x =>
            {
                notificationCount++;
                changes.Add(x);
            });
            orderBook.BidAskUpdatedStream.Subscribe(_ => notificationBidAskCount++);
            orderBook.TopLevelUpdatedStream.Subscribe(_ => notificationTopLevelCount++);

            source.StreamSnapshot();

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair, 499.4, 50, CryptoSide.Bid),
                CreateLevel(pair, 500.2, 400, CryptoSide.Ask)
            ));

            await Task.Delay(50);

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair, 499.5, 600, CryptoSide.Bid),
                CreateLevel(pair, 300.33, 3350, CryptoSide.Bid)
            ));

            await Task.Delay(50);

            source.StreamBulk(GetInsertBulk(
                CreateLevel(pair, 503.1, 3000, CryptoSide.Ask),
                CreateLevel(pair, 800.123, 1234, CryptoSide.Ask)
            ));

            await Task.Delay(50);

            source.StreamBulk(GetUpdateBulk(
                CreateLevel(pair, 499, 33, CryptoSide.Bid),
                CreateLevel(pair, 450, 33, CryptoSide.Bid),
                CreateLevel(pair, 501, 32, CryptoSide.Ask),
                CreateLevel(pair, 503.1, 32, CryptoSide.Ask),

                CreateLevel(pair, 100, null, CryptoSide.Bid),
                CreateLevel(pair, 900, null, CryptoSide.Ask)
            ));

            await Task.Delay(50);
            
            source.StreamBulk(GetUpdateBulk(
                CreateLevel(pair, 499.5, 100, CryptoSide.Bid)
            ));

            await Task.Delay(50);

            source.StreamBulk(GetUpdateBulk(
                CreateLevel(pair, 499.5, 200, CryptoSide.Bid)
            ));

            await Task.Delay(50);

            source.StreamBulk(GetUpdateBulk(
                CreateLevel(pair, 500.2, 22, CryptoSide.Ask)
            ));

            await Task.Delay(50);

            source.StreamBulk(GetDeleteBulk(
                CreateLevel(pair, 0, CryptoSide.Bid),
                CreateLevel(pair, 1, CryptoSide.Bid),
                CreateLevel(pair, 1000, CryptoSide.Ask),
                CreateLevel(pair, 999, CryptoSide.Ask)
            ));

            await Task.Delay(50);

            Assert.Equal(9, notificationCount);
            Assert.Equal(3, notificationBidAskCount);
            Assert.Equal(6, notificationTopLevelCount);

            var firstChange = changes.First();
            var secondChange = changes[1];

            Assert.Equal(0, firstChange.Levels.First().Price);
            Assert.Equal(501, firstChange.Levels.Last().Price);

            Assert.Equal(499.4, secondChange.Levels.First().Price);
            Assert.Equal(500.2, secondChange.Levels.Last().Price);
        }

        [Fact]
        public async Task AutoSnapshotReloading_ShouldWorkCorrectly()
        {
            var pair = "BTC/USD";
            var data = GetOrderBookSnapshotMockData(pair, 500);
            var source = new OrderBookSourceMock(data)
            {
                LoadSnapshotEnabled = true
            };

            var orderBook = new CryptoOrderBook(pair, source)
            {
                SnapshotReloadTimeout = TimeSpan.FromSeconds(1), 
                SnapshotReloadEnabled = true
            };

            await Task.Delay(TimeSpan.FromSeconds(6));

            Assert.Equal(pair, source.SnapshotLastPair);
            Assert.True(source.SnapshotCalledCount >= 4);
        }

        private OrderBookLevel[] GetOrderBookSnapshotMockData(string pair, int count)
        {
            var result = new List<OrderBookLevel>();

            for (int i = 0; i < count; i++)
            {
                var bid = CreateLevel(pair, i, count * 2 + i, CryptoSide.Bid);
                result.Add(bid);
            }

            
            for (int i = count*2; i > count; i--)
            {
                var ask = CreateLevel(pair, i, count * 4 + i, CryptoSide.Ask);
                result.Add(ask);
            }

            return result.ToArray();
        }
        private OrderBookLevelBulk GetInsertBulk(params OrderBookLevel[] levels)
        {
            return new OrderBookLevelBulk(OrderBookAction.Insert, levels);
        }

        private OrderBookLevelBulk GetUpdateBulk(params OrderBookLevel[] levels)
        {
            return new OrderBookLevelBulk(OrderBookAction.Update, levels);
        }

        private OrderBookLevelBulk GetDeleteBulk(params OrderBookLevel[] levels)
        {
            return new OrderBookLevelBulk(OrderBookAction.Delete, levels);
        }

        private OrderBookLevel CreateLevel(string pair, double price, double? amount, CryptoSide side)
        {
            return new OrderBookLevel(
                CreateKey(price,side),
                side,
                price,
                amount,
                3,
                pair == null ? null : CryptoPairsHelper.Clean(pair)
            );
        }

        private OrderBookLevel CreateLevel(string pair, double price, CryptoSide side)
        {
            return new OrderBookLevel(
                CreateKey(price,side),
                side,
                null,
                null,
                null,
                pair == null ? null : CryptoPairsHelper.Clean(pair)
            );
        }

        private string CreateKey(double price, CryptoSide side)
        {
            var sideSafe = side == CryptoSide.Bid ? "bid" : "ask";
            return $"{price}-{sideSafe}";
        }

        private class OrderBookSourceMock : OrderBookLevel2SourceBase
        {
            private readonly OrderBookLevel[] _snapshot;
            private readonly OrderBookLevelBulk[] _bulks;

            public int SnapshotCalledCount { get; private set; }
            public string SnapshotLastPair { get; private set; }

            public OrderBookSourceMock()
            {
                BufferInterval = TimeSpan.FromMilliseconds(10);
            }

            public OrderBookSourceMock(params OrderBookLevel[] snapshot)
            {
                BufferInterval = TimeSpan.FromMilliseconds(10);
                _snapshot = snapshot;
            }

            public OrderBookSourceMock(params OrderBookLevelBulk[] bulks)
            {
                BufferInterval = TimeSpan.FromMilliseconds(10);
                _bulks = bulks;
            }

            public void StreamSnapshot()
            {
                StreamSnapshot(_snapshot);
            }

            public void StreamBulks()
            {
                foreach (var bulk in _bulks)
                {
                    BufferData(bulk);
                }
            }

            public void StreamBulk(OrderBookLevelBulk bulk)
            {
                BufferData(bulk);
            }

            public override string ExchangeName => "mock";

            protected override Task<OrderBookLevel[]> LoadSnapshotInternal(string pair, int count = 1000)
            {
                SnapshotCalledCount++;
                SnapshotLastPair = pair;

                return Task.FromResult(new OrderBookLevel[0]);
            }

            protected override OrderBookLevelBulk[] ConvertData(object[] data)
            {
                return data.Cast<OrderBookLevelBulk>().ToArray();
            }
        }
    }
}
