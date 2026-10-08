#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;

using Grpc.Core;
using Grpc.Shared;
/*
using Microsoft.AspNetCore.Http.Features;
*/

namespace Grpc.AspNetCore.Server.Internal;

[DebuggerDisplay("{DebuggerToString(),nq}")]
[DebuggerTypeProxy(typeof(HttpListenerContextStreamReader<>.HttpListenerContextStreamReaderDebugView))]
internal sealed class HttpListenerContextStreamReader<TRequest> : IAsyncStreamReader<TRequest> where TRequest : class
{
    private readonly HttpListenerContextServerCallContext _serverCallContext;
    private readonly Func<DeserializationContext, TRequest> _deserializer;
    private readonly PipeReader _bodyReader;
    //private readonly IHttpListenerRequestLifetimeFeature _requestLifetimeFeature;
    private bool _completed;
    private long _readCount;
    private bool _endOfStream;

    public HttpListenerContextStreamReader(HttpListenerContextServerCallContext serverCallContext, Func<DeserializationContext, TRequest> deserializer)
    {
        _serverCallContext = serverCallContext;
        _deserializer = deserializer;

        // Copy HttpListenerContext values.
        // This is done to avoid a race condition when reading them from HttpListenerContext later when running in a separate thread.
        _bodyReader = _serverCallContext.PipeReader;
        // Copy lifetime feature because HttpListenerContext.RequestAborted on .NET 6 doesn't return the real cancellation token.
        //_requestLifetimeFeature = GrpcProtocolHelpers.GetRequestLifetimeFeature(_serverCallContext.HttpListenerContext);
    }

    public TRequest Current { get; private set; } = default!;

    public void Dispose() { }

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        async Task<bool> MoveNextAsync(ValueTask<TRequest?> readStreamTask)
        {
            return ProcessPayload(await readStreamTask);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        if (_completed /*|| _requestLifetimeFeature.RequestAborted.IsCancellationRequested TODO: Modified */)
        {
            return Task.FromException<bool>(new InvalidOperationException("Can't read messages after the request is complete."));
        }

        // Clear current before moving next. This prevents rooting the previous value while getting the next one.
        // In a long running stream this can allow the previous value to be GCed.
        Current = null!;

        var request = _bodyReader.ReadStreamMessageAsync(_serverCallContext, _deserializer, cancellationToken);
        if (!request.IsCompletedSuccessfully)
        {
            return MoveNextAsync(request);
        }

        return ProcessPayload(request.Result)
            ? CommonGrpcProtocolHelpers.TrueTask
            : CommonGrpcProtocolHelpers.FalseTask;
    }

    private bool ProcessPayload(TRequest? request)
    {
        // Stream is complete
        if (request == null)
        {
            _endOfStream = true;
            Current = null!;
            return false;
        }

        Current = request;
        Interlocked.Increment(ref _readCount);
        return true;
    }

    public void Complete()
    {
        _completed = true;
    }

    private string DebuggerToString() => $"ReadCount = {_readCount}, EndOfStream = {(_endOfStream ? "true" : "false")}";

    private sealed class HttpListenerContextStreamReaderDebugView
    {
        private readonly HttpListenerContextStreamReader<TRequest> _reader;

        public HttpListenerContextStreamReaderDebugView(HttpListenerContextStreamReader<TRequest> reader)
        {
            _reader = reader;
        }

        public ServerCallContext ServerCallContext => _reader._serverCallContext;
        public long ReadCount => _reader._readCount;
        public TRequest Current => _reader.Current;
        public bool EndOfStream => _reader._endOfStream;
    }
}
