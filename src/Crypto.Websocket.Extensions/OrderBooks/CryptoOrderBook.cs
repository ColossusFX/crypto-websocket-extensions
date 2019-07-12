﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Crypto.Websocket.Extensions.Logging;
using Crypto.Websocket.Extensions.Models;
using Crypto.Websocket.Extensions.OrderBooks.Models;
using Crypto.Websocket.Extensions.OrderBooks.Sources;
using Crypto.Websocket.Extensions.Utils;
using Crypto.Websocket.Extensions.Validations;

namespace Crypto.Websocket.Extensions.OrderBooks
{
    /// <summary>
    /// Cryptocurrency order book.
    /// Process order book data from one source and one target pair. 
    /// </summary>
    [DebuggerDisplay("CryptoOrderBook [{TargetPair}] bid: {BidPrice} ({_bidsBook.Count}) ask: {AskPrice} ({_asksBook.Count})")]
    public class CryptoOrderBook : ICryptoOrderBook
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

        private readonly IOrderBookLevel2Source _source;

        private readonly Subject<OrderBookChangeInfo> _bidAskUpdated = new Subject<OrderBookChangeInfo>();
        private readonly Subject<OrderBookChangeInfo> _topLevelUpdated = new Subject<OrderBookChangeInfo>();
        private readonly Subject<OrderBookChangeInfo> _orderBookUpdated = new Subject<OrderBookChangeInfo>();

        private readonly Dictionary<string, OrderBookLevel> _bidsBook = new Dictionary<string, OrderBookLevel>();
        private readonly Dictionary<string, OrderBookLevel> _asksBook = new Dictionary<string, OrderBookLevel>();

        private bool _isSnapshotLoaded = false;
        private Timer _snapshotReloadTimer;
        private TimeSpan _snapshotReloadTimeout = TimeSpan.FromMinutes(1);
        private bool _snapshotReloadEnabled = false;

        private IDisposable _subscriptionDiff;
        private IDisposable _subscriptionSnapshot;

        /// <summary>
        /// Cryptocurrency order book.
        /// Process order book data from one source per one target pair. 
        /// </summary>
        /// <param name="targetPair">Select target pair</param>
        /// <param name="source">Provide level 2 source data</param>
        public CryptoOrderBook(string targetPair, IOrderBookLevel2Source source)
        {
            CryptoValidations.ValidateInput(targetPair, nameof(targetPair));
            CryptoValidations.ValidateInput(source, nameof(source));

            TargetPairOriginal = targetPair;
            TargetPair = CryptoPairsHelper.Clean(targetPair);
            _source = source;

            Subscribe();
            RestartAutoSnapshotReloading();
        }

        /// <summary>
        /// Dispose background processing
        /// </summary>
        public void Dispose()
        {
            DeactivateAutoSnapshotReloading();
            _source.Dispose();
            _subscriptionDiff?.Dispose();
            _subscriptionSnapshot?.Dispose();
        }

        /// <summary>
        /// Origin exchange name
        /// </summary>
        public string ExchangeName => _source.ExchangeName;

        /// <summary>
        /// Target pair for this order book data
        /// </summary>
        public string TargetPair { get; }

        /// <summary>
        /// Originally provided target pair for this order book data
        /// </summary>
        public string TargetPairOriginal { get; }

        /// <summary>
        /// Time interval for auto snapshot reloading.
        /// Default 1 min. 
        /// </summary>
        public TimeSpan SnapshotReloadTimeout
        {
            get => _snapshotReloadTimeout;
            set
            {
                _snapshotReloadTimeout = value;
                RestartAutoSnapshotReloading();
            }
        }

        /// <summary>
        /// Whenever auto snapshot reloading feature is enabled.
        /// Disabled by default
        /// </summary>
        public bool SnapshotReloadEnabled
        {
            get => _snapshotReloadEnabled;
            set
            {
                _snapshotReloadEnabled = value;
                RestartAutoSnapshotReloading();
            }
        }

        /// <summary>
        /// Provide more info (on every change) whenever enabled. 
        /// Disabled by default
        /// </summary>
        public bool DebugEnabled { get; set; } = false;

        /// <summary>
        /// Streams data when top level bid or ask price was updated
        /// </summary>
        public IObservable<OrderBookChangeInfo> BidAskUpdatedStream => _bidAskUpdated.AsObservable();

        /// <summary>
        /// Streams data when top level bid or ask price or amount was updated
        /// </summary>
        public IObservable<OrderBookChangeInfo> TopLevelUpdatedStream => _topLevelUpdated.AsObservable();

        /// <summary>
        /// Streams data on every order book change (price or amount at any level)
        /// </summary>
        public IObservable<OrderBookChangeInfo> OrderBookUpdatedStream => _orderBookUpdated.AsObservable();

        /// <summary>
        /// Current bid side of the order book (ordered from higher to lower price)
        /// </summary>
        public OrderBookLevel[] BidLevels { get; private set; } = new OrderBookLevel[0];

        /// <summary>
        /// Current ask side of the order book (ordered from lower to higher price)
        /// </summary>
        public OrderBookLevel[] AskLevels { get; private set; } = new OrderBookLevel[0];

        /// <summary>
        /// All current levels together
        /// </summary>
        public OrderBookLevel[] Levels => BidLevels.Concat(AskLevels).ToArray();

        /// <summary>
        /// Current top level bid price
        /// </summary>
        public double BidPrice => BidLevels.FirstOrDefault()?.Price ?? 0;

        /// <summary>
        /// Current top level ask price
        /// </summary>
        public double AskPrice => AskLevels.FirstOrDefault()?.Price ?? 0;

        /// <summary>
        /// Current mid price
        /// </summary>
        public double MidPrice => (AskPrice + BidPrice) / 2;

        /// <summary>
        /// Current top level bid amount
        /// </summary>
        public double BidAmount => BidLevels.FirstOrDefault()?.Amount ?? 0;

        /// <summary>
        /// Current top level ask price
        /// </summary>
        public double AskAmount => AskLevels.FirstOrDefault()?.Amount ?? 0;

        /// <summary>
        /// Find bid level by provided price (returns null in case of not found)
        /// </summary>
        public OrderBookLevel FindBidLevelByPrice(double price)
        {
            return _bidsBook
                .Values
                .FirstOrDefault(x => CryptoMathUtils.IsSame(x.Price ?? 0, price));
        }

        /// <summary>
        /// Find ask level by provided price (returns null in case of not found)
        /// </summary>
        public OrderBookLevel FindAskLevelByPrice(double price)
        {
            return _asksBook
                .Values
                .FirstOrDefault(x => CryptoMathUtils.IsSame(x.Price ?? 0, price));
        }

        /// <summary>
        /// Find bid level by provided identification (returns null in case of not found)
        /// </summary>
        public OrderBookLevel FindBidLevelById(string id)
        {
            if (_bidsBook.ContainsKey(id))
                return _bidsBook[id];
            return null;
        }

        /// <summary>
        /// Find ask level by provided identification (returns null in case of not found)
        /// </summary>
        public OrderBookLevel FindAskLevelById(string id)
        {
            if (_asksBook.ContainsKey(id))
                return _asksBook[id];
            return null;
        }

        /// <summary>
        /// Find level by provided identification (returns null in case of not found).
        /// You need to specify side.
        /// </summary>
        public OrderBookLevel FindLevelById(string id, CryptoSide side)
        {
            if (side == CryptoSide.Undefined)
                return null;
            var collection = GetLevelsCollection(side);
            if (collection.ContainsKey(id))
                return collection[id];
            return null;
        }

        private void Subscribe()
        {
            _subscriptionSnapshot = _source
                .OrderBookSnapshotStream
                .Subscribe(HandleSnapshotSynchronized);

            _subscriptionDiff = _source
                .OrderBookStream
                .Subscribe(HandleDiffSynchronized);
        }

        private void HandleSnapshotSynchronized(OrderBookLevel[] levels)
        {
            var levelsForThis = levels
                .Where(x => TargetPair.Equals(x.Pair))
                .ToArray();
            if (!levelsForThis.Any())
            {
                // snapshot for different pair, ignore
                return;
            }

            var oldBid = BidPrice;
            var oldAsk = AskPrice;
            var oldBidAmount = BidAmount;
            var oldAskAmount = AskAmount;
            HandleSnapshot(levelsForThis);

            NotifyAboutBookChange(
            oldBid, oldAsk, 
            oldBidAmount, oldAskAmount,
            levelsForThis);
        }

        private void HandleDiffSynchronized(OrderBookLevelBulk[] bulks)
        {
            var forThis = bulks
                .Where(x => x.Levels.Any(y => TargetPair.Equals(y.Pair)))
                .ToArray();
            if (!forThis.Any())
            {
                // snapshot for different pair, ignore
                return;
            }

            var oldBid = BidPrice;
            var oldAsk = AskPrice;
            var oldBidAmount = BidAmount;
            var oldAskAmount = AskAmount;

            var allLevels = new List<OrderBookLevel>();
            foreach (var bulk in forThis)
            {
                var levelsForThis = bulk.Levels
                    .Where(x => TargetPair.Equals(x.Pair))
                    .ToArray();
                allLevels.AddRange(levelsForThis);
                HandleDiff(bulk, levelsForThis);
            }

            RecomputeAfterChange();

            NotifyAboutBookChange(
                oldBid, oldAsk, 
                oldBidAmount, oldAskAmount,
                allLevels.ToArray());
        }

        private void HandleSnapshot(OrderBookLevel[] levels)
        {
            _bidsBook.Clear();
            _asksBook.Clear();

            foreach (var level in levels)
            {
                if (level.Side == CryptoSide.Bid)
                    _bidsBook[level.Id] = level;

                if (level.Side == CryptoSide.Ask)
                    _asksBook[level.Id] = level;
            }

            RecomputeAfterChange();
            _isSnapshotLoaded = true;
        }

        private void HandleDiff(OrderBookLevelBulk bulk, OrderBookLevel[] correctLevels)
        {
            if (!_isSnapshotLoaded)
            {
                // snapshot is not loaded yet, ignore data
                return;
            }

            switch (bulk.Action)
            {
                case OrderBookAction.Insert:
                    InsertLevels(correctLevels);
                    break;
                case OrderBookAction.Update:
                    UpdateLevels(correctLevels);
                    break;
                case OrderBookAction.Delete:
                    DeleteLevels(correctLevels);
                    break;
                default:
                    return;
            }
        }

        private void InsertLevels(OrderBookLevel[] levels)
        {
            foreach (var level in levels)
            {
                if(level.Side == CryptoSide.Undefined)
                    continue;

                var collection = GetLevelsCollection(level.Side);
                InsertToCollection(collection, level.Id, level);
            }
        }

        private void UpdateLevels(OrderBookLevel[] levels)
        {
            foreach (var level in levels)
            {
                if(level.Side == CryptoSide.Undefined)
                    continue;
                
                var collection = GetLevelsCollection(level.Side);

                var existing = FindLevelById(level.Id, level.Side);
                if (existing == null)
                {
                    InsertToCollection(collection, level.Id, level);
                    continue;
                }

                var clone = new OrderBookLevel(
                    existing.Id,
                    existing.Side,
                    level.Price ?? existing.Price,
                    level.Amount ?? existing.Amount,
                    level.Count ?? existing.Count,
                    level.Pair ?? existing.Pair
                    );
                InsertToCollection(collection, level.Id, clone);
            }
        }

        private void InsertToCollection(Dictionary<string, OrderBookLevel> collection, string id, OrderBookLevel level)
        {
            if (collection == null)
                return;
            if (IsInvalidLevel(id, level))
            {
                //Log.Debug($"[ORDER BOOK {ExchangeName} {TargetPair}] " +
                //          $"Received weird level, ignoring. Id: {level.Id}, price: {level.Price}, amount: {level.Amount}");
                return;
            }

            collection[id] = level;
        }

        private static bool IsInvalidLevel(string id, OrderBookLevel level)
        {
            return string.IsNullOrWhiteSpace(id) ||
                   level.Price == null ||
                   level.Amount == null;
        }

        private void DeleteLevels(OrderBookLevel[] levels)
        {
            foreach (var level in levels)
            {
                if(level.Side == CryptoSide.Undefined)
                    continue;

                var collection = GetLevelsCollection(level.Side);
                collection.Remove(level.Id);
            }
        }

        private bool IsTargetPair(string pair)
        {
            return TargetPair.Equals(pair);
        }

        private void RecomputeAfterChange()
        {
            BidLevels = ComputeBidLevels();
            AskLevels = ComputeAskLevels();
        }

        private OrderBookLevel[] ComputeBidLevels()
        {
            var levels =  _bidsBook
                .Values
                .OrderByDescending(x => x.Price)
                .ToArray();
            return levels;
        }

        private OrderBookLevel[] ComputeAskLevels()
        {
            var levels =  _asksBook
                .Values
                .OrderBy(x => x.Price)
                .ToArray();
            return levels;
        }

        private Dictionary<string, OrderBookLevel> GetLevelsCollection(CryptoSide side)
        {
            if (side == CryptoSide.Undefined)
                return null;
            return side == CryptoSide.Bid ? 
                _bidsBook : 
                _asksBook;
        }

        private void NotifyAboutBookChange(double oldBid, double oldAsk, 
            double oldBidAmount, double oldAskAmount,
            OrderBookLevel[] levels)
        {
            var quotes = new CryptoQuotes(BidPrice, AskPrice, BidAmount, AskAmount);
            var clonedLevels = DebugEnabled ? levels.Select(x => x.Clone()).ToArray() : new OrderBookLevel[0];
            var change = new OrderBookChangeInfo(_source.ExchangeName, TargetPair, quotes, clonedLevels);

            _orderBookUpdated.OnNext(change);
            NotifyIfBidAskChanged(oldBid, oldAsk, change);
            NotifyIfTopLevelChanged(oldBid, oldAsk, oldBidAmount, oldAskAmount, change);
        }

        private void NotifyIfBidAskChanged(double oldBid, double oldAsk, OrderBookChangeInfo info)
        {
            if (PriceChanged(oldBid, oldAsk, info))
            {
                _bidAskUpdated.OnNext(info);
            }
        }

        private void NotifyIfTopLevelChanged(double oldBid, double oldAsk, 
            double oldBidAmount, double oldAskAmount,
            OrderBookChangeInfo info)
        {
            if (PriceChanged(oldBid, oldAsk, info) || AmountChanged(oldBidAmount, oldAskAmount, info))
            {
                _topLevelUpdated.OnNext(info);
            }
        }

        private static bool PriceChanged(double oldBid, double oldAsk, OrderBookChangeInfo info)
        {
            return !CryptoMathUtils.IsSame(oldBid, info.Quotes.Bid) || 
                   !CryptoMathUtils.IsSame(oldAsk, info.Quotes.Ask);
        }

        private static bool AmountChanged(double oldBidAmount, double oldAskAmount, OrderBookChangeInfo info)
        {
            return !CryptoMathUtils.IsSame(oldBidAmount, info.Quotes.BidAmount) || 
                   !CryptoMathUtils.IsSame(oldAskAmount, info.Quotes.AskAmount);
        }

        private async Task ReloadSnapshot()
        {
            try
            {
                if (!_source.LoadSnapshotEnabled)
                {
                    // snapshot loading disabled on the source, do nothing
                    return;
                }

                DeactivateAutoSnapshotReloading();
                await _source.LoadSnapshot(TargetPairOriginal, 10000);
            }
            catch (Exception e)
            {
                Log.Debug(e, $"[ORDER BOOK {ExchangeName} {TargetPair}] " +
                            $"Failed to auto reload snapshot for pair '{TargetPair}', " +
                            $"error: {e.Message}");
            }
            finally
            {
                RestartAutoSnapshotReloading();
            }
        }

        private void RestartAutoSnapshotReloading()
        {
            DeactivateAutoSnapshotReloading();

            if (!_snapshotReloadEnabled)
            {
                // snapshot reloading disabled, do not start timer
                return;
            }

            var timerMs = (int)SnapshotReloadTimeout.TotalMilliseconds;
            _snapshotReloadTimer = new Timer(async _ => await ReloadSnapshot(), 
                null, timerMs, timerMs);
        }

        private void DeactivateAutoSnapshotReloading()
        {
            _snapshotReloadTimer?.Dispose();
            _snapshotReloadTimer = null;
        }
    }
}
