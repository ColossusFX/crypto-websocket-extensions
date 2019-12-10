![Logo](cwe_logo.png)
# Cryptocurrency websocket extensions [![Build Status](https://travis-ci.org/Marfusios/crypto-websocket-extensions.svg?branch=master)](https://travis-ci.org/Marfusios/crypto-websocket-extensions) [![NuGet version](https://badge.fury.io/nu/Crypto.Websocket.Extensions.svg)](https://badge.fury.io/nu/Crypto.Websocket.Extensions)

This is a library that provides extensions to cryptocurrency websocket exchange clients. 

It helps to unify data models and usage of more clients together. 

[Releases and breaking changes](https://github.com/Marfusios/crypto-websocket-extensions/releases)

### License: 
    Apache License 2.0

### Features

* installation via NuGet
    * full (with all exchange clients) - [Crypto.Websocket.Extensions](https://www.nuget.org/packages/Crypto.Websocket.Extensions)
	* core (only interfaces and features) - [Crypto.Websocket.Extensions.Core](https://www.nuget.org/packages/Crypto.Websocket.Extensions.Core)
* targeting .NET Standard 2.0 (.NET Core, Linux/MacOS compatible)
* reactive extensions ([Rx.NET](https://github.com/Reactive-Extensions/Rx.NET))
* integrated logging abstraction ([LibLog](https://github.com/damianh/LibLog))

### Supported exchanges

| Logo | Name | Websocket client |
| ------------- | ------------- |:------:|
| [![bitfinex](https://user-images.githubusercontent.com/1294454/27766244-e328a50c-5ed2-11e7-947b-041416579bb3.jpg)](https://www.bitfinex.com)  | [Bitfinex](https://www.bitfinex.com)  | [bitfinex-client-websocket](https://github.com/Marfusios/bitfinex-client-websocket) |
| [![bitmex](https://user-images.githubusercontent.com/1294454/27766319-f653c6e6-5ed4-11e7-933d-f0bc3699ae8f.jpg)](https://www.bitmex.com/register/qGWwBG)  | [BitMEX](https://www.bitmex.com/register/qGWwBG)  | [bitmex-client-websocket](https://github.com/Marfusios/bitmex-client-websocket) |
| [![binance](https://user-images.githubusercontent.com/1294454/29604020-d5483cdc-87ee-11e7-94c7-d1a8d9169293.jpg)](https://www.binance.com/?ref=21773680)  | [Binance](https://www.binance.com/?ref=21773680)  | [binance-client-websocket](https://github.com/Marfusios/binance-client-websocket) |
| [![coinbase](https://user-images.githubusercontent.com/1294454/41764625-63b7ffde-760a-11e8-996d-a6328fa9347a.jpg)](https://www.coinbase.com/join/kotas_4)  | [Coinbase](https://www.coinbase.com/join/kotas_4)  | [coinbase-client-websocket](https://github.com/Marfusios/coinbase-client-websocket) |

## Extensions

### Order book

* `CryptoOrderBook` class - unified order book across all exchanges
* support for level2 market data (only differences in order book)
* provides streams:
    * `OrderBookUpdatedStream` - streams on an every order book update
    * `BidAskUpdatedStream` - streams when bid or ask price changed (top level of the order book)
	* `TopLevelUpdatedStream` - streams when bid or ask price/amount changed (top level of the order book)
* provides properties and methods:
    * `BidLevels` and `AskLevels` - ordered array of current state of the order book
    * `FindLevelByPrice` and `FindLevelById` - returns specific order book level

Usage:

```csharp
var url = BitmexValues.ApiWebsocketUrl;
var communicator = new BitmexWebsocketCommunicator(url);
var client = new BitmexWebsocketClient(communicator);

var pair = "XBTUSD";

var source = new BitmexOrderBookSource(client);
var orderBook = new CryptoOrderBook(pair, source);

// orderBook.BidAskUpdatedStream.Subscribe(xxx)
orderBook.OrderBookUpdatedStream.Subscribe(quotes =>
{
    var currentBid = orderBook.BidPrice;
    var currentAsk = orderBook.AskPrice;

    var bids = orderBook.BidLevels;
    // xxx
});
        
await communicator.Start();
```

### Trades

* `ITradeSource` - unified trade info stream across all exchanges

### Orders (authenticated)

* `CryptoOrders` class - unified orders status across all exchanges with features:
    * orders view and searching - only executed, search by id, client id, etc.
    * our vs all orders - using client id prefix to distinguish between orders

### Position (authenticated)

* `IPositionSource` - unified position info stream across all exchanges

### Wallet (authenticated)

* `IWalletSource` - unified wallet status stream across all exchanges



---

More usage examples:
* unit tests ([link](test/Crypto.Websocket.Extensions.Tests))
* integration tests ([link](test_integration/Crypto.Websocket.Extensions.Tests.Integration))
* console sample ([link](test_integration/Crypto.Websocket.Extensions.Sample/Program.cs))

**Pull Requests are welcome!**

### Powerfull Rx.NET

Don't forget that you can do pretty nice things with reactive extensions and observables. 
For example, if you want to check latest bid/ask prices from all exchanges all together, 
you can do something like this: 

```csharp
Observable.CombineLatest(new[]
            {
                bitmexOrderBook.BidAskUpdatedStream,
                bitfinexOrderBook.BidAskUpdatedStream,
                binanceOrderBook.BidAskUpdatedStream,
            })
            .Subscribe(HandleQuoteChanged);

// Method HandleQuoteChanged(IList<CryptoQuotes> quotes)
// will be called on every exchange's price change
```


### Multi-threading

Observables from Reactive Extensions are single threaded by default. It means that your code inside subscriptions is called synchronously and as soon as the message comes from websocket API. It brings a great advantage of not to worry about synchronization, but if your code takes a longer time to execute it will block the receiving method, buffer the messages and may end up losing messages. For that reason consider to handle messages on the other thread and unblock receiving thread as soon as possible. I've prepared a few examples for you: 

#### Default behavior

Every subscription code is called on a main websocket thread. Every subscription is synchronized together. No parallel execution. It will block the receiving thread. 

```csharp
client
    .Streams
    .TradesStream
    .Subscribe(trade => { code1 });

client
    .Streams
    .BookStream
    .Subscribe(book => { code2 });

// 'code1' and 'code2' are called in a correct order, according to websocket flow
// ----- code1 ----- code1 ----- ----- code1
// ----- ----- code2 ----- code2 code2 -----
```

#### Parallel subscriptions 

Every single subscription code is called on a separate thread. Every single subscription is synchronized, but different subscriptions are called in parallel. 

```csharp
client
    .Streams
    .TradesStream
    .ObserveOn(TaskPoolScheduler.Default)
    .Subscribe(trade => { code1 });

client
    .Streams
    .BookStream
    .ObserveOn(TaskPoolScheduler.Default)
    .Subscribe(book => { code2 });

// 'code1' and 'code2' are called in parallel, do not follow websocket flow
// ----- code1 ----- code1 ----- code1 -----
// ----- code2 code2 ----- code2 code2 code2
```

 #### Parallel subscriptions with synchronization

In case you want to run your subscription code on the separate thread but still want to follow websocket flow through every subscription, use synchronization with gates: 

```csharp
private static readonly object GATE1 = new object();
client
    .Streams
    .TradesStream
    .ObserveOn(TaskPoolScheduler.Default)
    .Synchronize(GATE1)
    .Subscribe(trade => { code1 });

client
    .Streams
    .BookStream
    .ObserveOn(TaskPoolScheduler.Default)
    .Synchronize(GATE1)
    .Subscribe(book => { code2 });

// 'code1' and 'code2' are called concurrently and follow websocket flow
// ----- code1 ----- code1 ----- ----- code1
// ----- ----- code2 ----- code2 code2 ----
```

### Async/Await integration

Using `async/await` in your subscribe methods is a bit tricky. Subscribe from Rx.NET doesn't `await` tasks, 
so it won't block stream execution and cause sometimes undesired concurrency. For example: 

```csharp
client
    .Streams
    .TradesStream
    .Subscribe(async trade => {
        // do smth 1
        await Task.Delay(5000); // waits 5 sec, could be HTTP call or something else
        // do smth 2
    });
```

That `await Task.Delay` won't block stream and subscribe method will be called multiple times concurrently. 
If you want to buffer messages and process them one-by-one, then use this: 

```csharp
client
    .Streams
    .TradesStream
    .Select(trade => Observable.FromAsync(async () => {
        // do smth 1
        await Task.Delay(5000); // waits 5 sec, could be HTTP call or something else
        // do smth 2
    }))
    .Concat() // executes sequentially
    .Subscribe();
```

If you want to process them concurrently (avoid synchronization), then use this

```csharp
client
    .Streams
    .TradesStream
    .Select(trade => Observable.FromAsync(async () => {
        // do smth 1
        await Task.Delay(5000); // waits 5 sec, could be HTTP call or something else
        // do smth 2
    }))
    .Merge() // executes concurrently
    // .Merge(4) you can limit concurrency with a parameter
    // .Merge(1) is same as .Concat()
    // .Merge(0) is invalid (throws exception)
    .Subscribe();
```

More info on [Github issue](https://github.com/dotnet/reactive/issues/459).

Don't worry about websocket connection, those sequential execution via `.Concat()` or `.Merge(1)` has no effect on receiving messages. 
It won't affect receiving thread, only buffers messages inside `TradesStream`. 

But beware of [producer-consumer problem](https://en.wikipedia.org/wiki/Producer%E2%80%93consumer_problem) when the consumer will be too slow. Here is a [StackOverflow issue](https://stackoverflow.com/questions/11010602/with-rx-how-do-i-ignore-all-except-the-latest-value-when-my-subscribe-method-is) 
with an example how to ignore/discard buffered messages and always process only the last one. 

### Available for help
I do consulting, please don't hesitate to contact me if you have a custom solution you would like me to implement ([web](http://mkotas.cz/), 
<m@mkotas.cz>)

Donations gratefully accepted.
* [![Donate with Bitcoin](https://en.cryptobadges.io/badge/small/1HfxKZhvm68qK3gE8bJAdDBWkcZ2AFs9pw)](https://en.cryptobadges.io/donate/1HfxKZhvm68qK3gE8bJAdDBWkcZ2AFs9pw)
* [![Donate with Litecoin](https://en.cryptobadges.io/badge/small/LftdENE8DTbLpV6RZLKLdzYzVU82E6dz4W)](https://en.cryptobadges.io/donate/LftdENE8DTbLpV6RZLKLdzYzVU82E6dz4W)
* [![Donate with Ethereum](https://en.cryptobadges.io/badge/small/0xb9637c56b307f24372cdcebd208c0679d4e48a47)](https://en.cryptobadges.io/donate/0xb9637c56b307f24372cdcebd208c0679d4e48a47)
