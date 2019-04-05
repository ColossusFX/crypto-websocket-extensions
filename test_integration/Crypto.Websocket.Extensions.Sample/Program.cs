﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Binance.Client.Websocket;
using Binance.Client.Websocket.Client;
using Binance.Client.Websocket.Subscriptions;
using Binance.Client.Websocket.Websockets;
using Bitfinex.Client.Websocket;
using Bitfinex.Client.Websocket.Client;
using Bitfinex.Client.Websocket.Websockets;
using Bitmex.Client.Websocket;
using Bitmex.Client.Websocket.Client;
using Bitmex.Client.Websocket.Websockets;
using Crypto.Websocket.Extensions.Models;
using Crypto.Websocket.Extensions.OrderBooks;
using Crypto.Websocket.Extensions.OrderBooks.Models;
using Crypto.Websocket.Extensions.OrderBooks.Sources;
using Serilog;
using Serilog.Events;

namespace Crypto.Websocket.Extensions.Sample
{
    class Program
    {
        private static readonly ManualResetEvent ExitEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            InitLogging();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
            AssemblyLoadContext.Default.Unloading += DefaultOnUnloading;
            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            Console.WriteLine("|========================|");
            Console.WriteLine("|  WEBSOCKET EXTENSIONS  |");
            Console.WriteLine("|========================|");
            Console.WriteLine();

            Log.Debug("====================================");
            Log.Debug("              STARTING              ");
            Log.Debug("====================================");

            RunEverything().Wait();

            ExitEvent.WaitOne();

            Log.Debug("====================================");
            Log.Debug("              STOPPING              ");
            Log.Debug("====================================");
            Log.CloseAndFlush();
        }

        private static async Task RunEverything()
        {
            var bitmexOb = await StartBitmex("XBTUSD");
            var bitfinexOb = await StartBitfinex("BTCUSD");
            var binanceOb = await StartBinance("BTCUSDT");

            Observable.CombineLatest(new[]
                {
                    bitmexOb.BidAskUpdatedStream,
                    bitfinexOb.BidAskUpdatedStream,
                    binanceOb.BidAskUpdatedStream,
                })
                .Subscribe(HandleQuoteChanged);
        }

        private static void HandleQuoteChanged(IList<OrderBookChangeInfo> quotes)
        {
            var formattedMessages = quotes
                .Select(x => $"{x.ExchangeName.ToUpper()} {x.Quotes.Bid + "/" + x.Quotes.Ask,16}")
                .Select(x => $"{x,30}")
                .ToArray();

            var msg = string.Join(" | ", formattedMessages);
            Log.Information($"Quotes changed:  {msg}");
        }


        private static async Task<CryptoOrderBook> StartBitmex(string pair)
        {
            var url = BitmexValues.ApiWebsocketUrl;
            var communicator = new BitmexWebsocketCommunicator(url) { Name = "Bitmex" };
            var client = new BitmexWebsocketClient(communicator);

            var source = new BitmexOrderBookSource(client);
            var orderBook = new CryptoOrderBook(pair, source);
            await communicator.Start();

            // Send subscription request to order book data
            await client.Send(new Bitmex.Client.Websocket.Requests.BookSubscribeRequest(pair));

            return orderBook;
        }

        private static async Task<CryptoOrderBook> StartBitfinex(string pair)
        {
            var url = BitfinexValues.ApiWebsocketUrl;
            var communicator = new BitfinexWebsocketCommunicator(url) { Name = "Bitfinex" };
            var client = new BitfinexWebsocketClient(communicator);

            var source = new BitfinexOrderBookSource(client);
            var orderBook = new CryptoOrderBook(pair, source);
            await communicator.Start();

            // Send subscription request to order book data
            await client.Send(new Bitfinex.Client.Websocket.Requests.Subscriptions.BookSubscribeRequest(pair));

            return orderBook;
        }

        private static async Task<CryptoOrderBook> StartBinance(string pair)
        {
            var url = BinanceValues.ApiWebsocketUrl;
            var communicator = new BinanceWebsocketCommunicator(url) { Name = "Binance" };
            var client = new BinanceWebsocketClient(communicator);

            client.SetSubscriptions(
                new OrderBookDiffSubscription(pair)
            );

            var source = new BinanceOrderBookSource(client);
            var orderBook = new CryptoOrderBook(pair, source);

            await communicator.Start();

            // Binance is special
            // We need to load snapshot in advance manually via REST call
            await source.LoadSnapshot(pair);

            return orderBook;
        }



        private static void InitLogging()
        {
            var executingDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var logPath = Path.Combine(executingDir, "logs", "verbose.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .WriteTo.ColoredConsole(LogEventLevel.Debug)
                .CreateLogger();
        }

        private static void CurrentDomainOnProcessExit(object sender, EventArgs eventArgs)
        {
            Log.Warning("Exiting process");
            ExitEvent.Set();
        }

        private static void DefaultOnUnloading(AssemblyLoadContext assemblyLoadContext)
        {
            Log.Warning("Unloading process");
            ExitEvent.Set();
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Warning("Canceling process");
            e.Cancel = true;
            ExitEvent.Set();
        }
    }
}
