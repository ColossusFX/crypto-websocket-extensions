using System;
using System.Collections.Concurrent;
using Crypto.Websocket.Extensions.Core.Positions.Models;
using Crypto.Websocket.Extensions.Core.Positions.Sources;
using Crypto.Websocket.Extensions.Core.Validations;
using Crypto.Websocket.Extensions.Logging;
using Ftx.Client.Websocket.Client;

namespace Crypto.Websocket.Extensions.Positions.Sources
{
    public class FtxPositionSource: PositionSourceBase
    {
        private static readonly ILog Log = LogProvider.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, CryptoPosition> _positions = new ConcurrentDictionary<string, CryptoPosition>();

        private FtxWebsocketClient _client;
        private IDisposable _subscription;

        /// <inheritdoc />
        public FtxPositionSource(FtxWebsocketClient client)
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

        }
    }
}