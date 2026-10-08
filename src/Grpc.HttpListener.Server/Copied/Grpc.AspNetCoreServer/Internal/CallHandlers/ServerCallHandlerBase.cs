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

using System.Diagnostics.CodeAnalysis;
using Grpc.Core;
using Grpc.Shared.Server;
using System.Net;

/*
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Timeouts;
#endif
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
*/
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Grpc.AspNetCore.Server.Internal.CallHandlers;

public abstract class ServerCallHandlerBase<[DynamicallyAccessedMembers(GrpcProtocolConstants.ServiceAccessibility)] TService, TRequest, TResponse>
    where TService : class
    where TRequest : class
    where TResponse : class
{
    private const string LoggerName = "Grpc.AspNetCore.Server.ServerCallHandler";

    protected ServerMethodInvokerBase<TService, TRequest, TResponse> MethodInvoker { get; }
    protected ILogger Logger { get; }

    protected ServerCallHandlerBase(
        ServerMethodInvokerBase<TService, TRequest, TResponse> methodInvoker,
        ILoggerFactory loggerFactory)
    {
        MethodInvoker = methodInvoker;
        Logger = loggerFactory.CreateLogger(LoggerName);
    }

    public Task HandleCallAsync(HttpListenerContext HttpListenerContext)
    {
        if (GrpcProtocolHelpers.IsInvalidContentType(HttpListenerContext, out var error))
        {
            return ProcessInvalidContentTypeRequest(HttpListenerContext, error);
        }

/*
        TODO: This avoids processing normal http/2 requests
        if (!GrpcProtocolConstants.IsHttp2(HttpListenerContext.Request.ProtocolVersion)
            && !GrpcProtocolConstants.IsHttp3(HttpListenerContext.Request.ProtocolVersion))
*/            
        if (!GrpcProtocolConstants.IsHttp1(HttpListenerContext.Request.ProtocolVersion))
        {
            return ProcessNonHttp2Request(HttpListenerContext);
        }

        var serverCallContext = new HttpListenerContextServerCallContext(HttpListenerContext, MethodInvoker.Options, typeof(TRequest), typeof(TResponse), Logger);
//        HttpListenerContext.Features.Set<IServerCallContextFeature>(serverCallContext);

        GrpcProtocolHelpers.AddProtocolHeaders(HttpListenerContext.Response);

        try
        {
            serverCallContext.Initialize();

            var handleCallTask = HandleCallAsyncCore(HttpListenerContext, serverCallContext);

            if (handleCallTask.IsCompletedSuccessfully)
            {
                return serverCallContext.EndCallAsync();
            }
            else
            {
                return AwaitHandleCall(serverCallContext, MethodInvoker.Method, handleCallTask);
            }
        }
        catch (Exception ex)
        {
            return serverCallContext.ProcessHandlerErrorAsync(ex, MethodInvoker.Method.Name);
        }

        static async Task AwaitHandleCall(HttpListenerContextServerCallContext serverCallContext, Method<TRequest, TResponse> method, Task handleCall)
        {
            try
            {
                await handleCall;
                await serverCallContext.EndCallAsync();
            }
            catch (Exception ex)
            {
                await serverCallContext.ProcessHandlerErrorAsync(ex, method.Name);
            }
        }
    }

    protected abstract Task HandleCallAsyncCore(HttpListenerContext HttpListenerContext, HttpListenerContextServerCallContext serverCallContext);

    /// <summary>
    /// This should only be called from client streaming calls
    /// </summary>
    /// <param name="HttpListenerContext"></param>
    protected void DisableMinRequestBodyDataRateAndMaxRequestBodySize(HttpListenerContext HttpListenerContext)
    {
        /*
        var minRequestBodyDataRateFeature = HttpListenerContext.Features.Get<IHttpMinRequestBodyDataRateFeature>();
        if (minRequestBodyDataRateFeature != null)
        {
            minRequestBodyDataRateFeature.MinDataRate = null;
        }

        var maxRequestBodySizeFeature = HttpListenerContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature != null)
        {
            if (!maxRequestBodySizeFeature.IsReadOnly)
            {
                maxRequestBodySizeFeature.MaxRequestBodySize = null;
            }
            else
            {
                // IsReadOnly could be true if middleware has already started reading the request body
                // In that case we can't disable the max request body size for the request stream
                GrpcServerLog.UnableToDisableMaxRequestBodySize(Logger);
            }
        }
        */
    }

#if NET8_0_OR_GREATER
    protected void DisableRequestTimeout(HttpListenerContext HttpListenerContext)
    {
        /*

        TODO: Not having a timeout is important - bu we commented it...

        // Disable global request timeout on streaming methods.
        var requestTimeoutFeature = HttpListenerContext.Features.Get<IHttpListenerRequestTimeoutFeature>();
        if (requestTimeoutFeature is not null)
        {
            // Don't disable if the endpoint has explicit timeout metadata.
            var endpoint = HttpListenerContext.GetEndpoint();
            if (endpoint is not null)
            {
                if (endpoint.Metadata.GetMetadata<RequestTimeoutAttribute>() is not null ||
                    endpoint.Metadata.GetMetadata<RequestTimeoutPolicy>() is not null)
                {
                    return;
                }
            }

            requestTimeoutFeature.DisableTimeout();
        }
        */
    }
#endif

    private async Task ProcessNonHttp2Request(HttpListenerContext HttpListenerContext)
    {
        GrpcServerLog.UnsupportedRequestProtocol(Logger, HttpListenerContext.Request.ProtocolVersion.Major.ToString());

        var protocolError = $"Request protocol '{HttpListenerContext.Request.ProtocolVersion.Major}' is not supported.";
        await GrpcProtocolHelpers.BuildHttpErrorResponseAsync(HttpListenerContext.Response, 426, StatusCode.Internal, protocolError);
        HttpListenerContext.Response.Headers[HeaderNames.Upgrade] = GrpcProtocolConstants.Http2Protocol;
    }

    private async Task ProcessInvalidContentTypeRequest(HttpListenerContext HttpListenerContext, string error)
    {
        // This might be a CORS preflight request and CORS middleware hasn't been configured
        if (GrpcProtocolHelpers.IsCorsPreflightRequest(HttpListenerContext))
        {
            GrpcServerLog.UnhandledCorsPreflightRequest(Logger);

            await GrpcProtocolHelpers.BuildHttpErrorResponseAsync(HttpListenerContext.Response, 405, StatusCode.Internal, "Unhandled CORS preflight request received. CORS may not be configured correctly in the application.");
            HttpListenerContext.Response.Headers[HeaderNames.Allow] = HttpMethod.Post.Method;
        }
        else
        {
            GrpcServerLog.UnsupportedRequestContentType(Logger, HttpListenerContext.Request.ContentType);

            await GrpcProtocolHelpers.BuildHttpErrorResponseAsync(HttpListenerContext.Response, 415, StatusCode.Internal, error);
        }
    }
}
