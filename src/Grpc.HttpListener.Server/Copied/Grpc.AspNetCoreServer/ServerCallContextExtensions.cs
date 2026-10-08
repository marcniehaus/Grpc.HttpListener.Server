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

using System.Net;
using Grpc.AspNetCore.Server.Internal;
using Grpc.Shared;
/*
using Microsoft.AspNetCore.Http;
*/

namespace Grpc.Core;

/// <summary>
/// Extension methods for ServerCallContext.
/// </summary>
public static class ServerCallContextExtensions
{
    internal const string HttpListenerContextKey = "__HttpListenerContext";

    /// <summary>
    /// Retrieve the <see cref="HttpListenerContext"/> from the a call's <see cref="ServerCallContext"/>.
    /// The HttpListenerContext is only available when gRPC services are hosted by ASP.NET Core. An error will be
    /// thrown if this method is used outside of ASP.NET Core.
    /// Note that read-only usage of HttpListenerContext is recommended as changes to the HttpListenerContext are not synchronized
    /// with the ServerCallContext.
    /// </summary>
    /// <param name="serverCallContext">The <see cref="ServerCallContext"/>.</param>
    /// <returns>The call's <see cref="HttpListenerContext"/>.</returns>
    public static HttpListenerContext GetHttpListenerContext(this ServerCallContext serverCallContext)
    {
        ArgumentNullThrowHelper.ThrowIfNull(serverCallContext);

        // Attempt to quickly get HttpListenerContext from known call context type.
        if (serverCallContext is HttpListenerContextServerCallContext HttpListenerContextServerCallContext)
        {
            return HttpListenerContextServerCallContext.HttpListenerContext;
        }

        // Fallback to getting HttpListenerContext from user state.
        // This is to support custom gRPC invokers that replace the default server call context.
        // They must place the HttpListenerContext in UserState with the `__HttpListenerContext` key.
        if (serverCallContext.UserState != null &&
            serverCallContext.UserState.TryGetValue(HttpListenerContextKey, out var c) &&
            c is HttpListenerContext HttpListenerContext)
        {
            return HttpListenerContext;
        }

        throw new InvalidOperationException("Could not get HttpListenerContext from ServerCallContext. HttpListenerContext can only be accessed when gRPC services are hosted by ASP.NET Core.");
    }
}
