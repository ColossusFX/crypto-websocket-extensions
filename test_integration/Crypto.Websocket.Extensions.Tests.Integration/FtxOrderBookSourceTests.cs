using System;
using System.Threading.Tasks;
using Crypto.Websocket.Extensions.Core.OrderBooks;
using Crypto.Websocket.Extensions.OrderBooks.Sources;
using Ftx.Client.Websocket;
using Ftx.Client.Websocket.Client;
using Ftx.Client.Websocket.Requests;
using Ftx.Client.Websocket.Websockets;
using Xunit;

namespace Crypto.Websocket.Extensions.Tests.Integration
{
    public class FtxOrderBookSourceTests
    {
        [Fact]
        public async Task ConnectToSource_ShouldHandleOrderBookCorrectly()
        {
            var url = FtxValues.ApiWebsocketUrl;
            using (var communicator = new FtxWebsocketCommunicator(url))
            {
                using (var client = new FtxWebsocketClient(communicator))
                {
                    var pair = "BTC-PERP";

                    var source = new FtxOrderBookSource(client);
                    var orderBook = new CryptoOrderBook(pair, source);
                    
                    await communicator.Start();

                    client.Send(new BookSubscribeRequest(pair));

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    Assert.True(orderBook.BidPrice > 0);
                    Assert.True(orderBook.AskPrice > 0);

                    Assert.NotEmpty(orderBook.BidLevels);
                    Assert.NotEmpty(orderBook.AskLevels);
                }
            }
        }
    }
}