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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Net;
using Grpc.Core;
using Grpc.Shared;
/*
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
*/
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Collections.Specialized;
using System.IO.Pipelines;

namespace Grpc.AspNetCore.Server.Internal;

internal static class GrpcProtocolHelpers
{
    public static bool TryDecodeTimeout(StringValues values, out TimeSpan timeout)
    {
        const long TicksPerMicrosecond = 10; // 1 microsecond = 10 ticks
        const long NanosecondsPerTick = 100; // 1 nanosecond = 0.01 ticks

        if (values.Count == 1)
        {
            var timeoutHeader = values.ToString();
            if (timeoutHeader.Length >= 2)
            {
                var timeoutUnit = timeoutHeader[timeoutHeader.Length - 1];
                if (int.TryParse(timeoutHeader.AsSpan(0, timeoutHeader.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var timeoutValue))
                {
                    switch (timeoutUnit)
                    {
                        case 'H':
                            timeout = TimeSpan.FromHours(timeoutValue);
                            return true;
                        case 'M':
                            timeout = TimeSpan.FromMinutes(timeoutValue);
                            return true;
                        case 'S':
                            timeout = TimeSpan.FromSeconds(timeoutValue);
                            return true;
                        case 'm':
                            timeout = TimeSpan.FromMilliseconds(timeoutValue);
                            return true;
                        case 'u':
                            timeout = TimeSpan.FromTicks(timeoutValue * TicksPerMicrosecond);
                            return true;
                        case 'n':
                            timeout = TimeSpan.FromTicks(timeoutValue / NanosecondsPerTick);
                            return true;
                    }
                }
            }
        }

        timeout = TimeSpan.Zero;
        return false;
    }

    public static bool IsInvalidContentType(HttpListenerContext HttpListenerContext, [NotNullWhen(true)] out string? error)
    {
        if (HttpListenerContext.Request.ContentType == null)
        {
            error = "Content-Type is missing from the request.";
            return true;
        }
        //TODO: Now we cannot handle normal gRPC calls anymore...
        else if (!CommonGrpcProtocolHelpers.IsContentType(GrpcProtocolConstants.GrpcWebContentType, HttpListenerContext.Request.ContentType))
        {
            error = $"Content-Type '{HttpListenerContext.Request.ContentType}' is not supported.";
            return true;
        }

        error = null;
        return false;
    }

    public static bool IsCorsPreflightRequest(HttpListenerContext HttpListenerContext)
    {
        return string.Equals(HttpListenerContext.Request.HttpMethod, HttpMethod.Options.Method, StringComparison.InvariantCultureIgnoreCase) &&
            HttpListenerContext.Request.Headers.AllKeys.Contains(HeaderNames.AccessControlRequestMethod);
    }

    public static async Task BuildHttpErrorResponseAsync(HttpListenerResponse response, int httpStatusCode, StatusCode grpcStatusCode, string message)
    {
        response.StatusCode = httpStatusCode;
        await SetStatusAsync(GetTrailersDestination(response, PipeWriter.Create(response.OutputStream)), new Status(grpcStatusCode, message));
    }

    public static byte[] ParseBinaryHeader(string base64)
    {
        string decodable;
        switch (base64.Length % 4)
        {
            case 0:
                // base64 has the required padding
                decodable = base64;
                break;
            case 2:
                // 2 chars padding
                decodable = base64 + "==";
                break;
            case 3:
                // 3 chars padding
                decodable = base64 + "=";
                break;
            default:
                // length%4 == 1 should be illegal
                throw new FormatException("Invalid base64 header value");
        }

        return Convert.FromBase64String(decodable);
    }

    public static void AddProtocolHeaders(HttpListenerResponse response)
    {
        response.ContentType = GrpcProtocolConstants.GrpcWebContentType;
    }

    public static Task SetStatusAsync(Func<string, string, Task> writeTrailer, Status status)
    {
        // Overwrite any previously set status
        return writeTrailer(GrpcProtocolConstants.StatusTrailer, status.StatusCode.ToTrailerString());

/*
        TODO: I don't know the trailers format

        string? escapedDetail;
        if (!string.IsNullOrEmpty(status.Detail))
        {
            // https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md#responses
            // The value portion of Status-Message is conceptually a Unicode string description of the error,
            // physically encoded as UTF-8 followed by percent-encoding.
            escapedDetail = PercentEncodingHelpers.PercentEncode(status.Detail);
        }
        else
        {
            escapedDetail = null;
        }

        destination[GrpcProtocolConstants.MessageTrailer] = escapedDetail;

*/     
    }

    public static Func<string, string, Task> GetTrailersDestination(HttpListenerResponse response, PipeWriter writer)
    {
        if (writer.UnflushedBytes > 0)
        {
            /*
            // The response has content so write trailers to a trailing HEADERS frame
            var feature = response.HttpListenerContext.Features.Get<IHttpListenerResponseTrailersFeature>();
            if (feature?.Trailers == null || feature.Trailers.IsReadOnly)
            {
                throw new InvalidOperationException("Trailers are not supported for this response. The server may not support gRPC.");
            }

            return feature.Trailers;
            */

            return async (s, w) =>
            {
                var prefix = new byte[]{0x80, 0x0, 0x0, 0x0, 0x10}; //TODO: From wireshark - but what does it mean?!
                var header = Encoding.ASCII.GetBytes($"{s.ToLower()}: {w.ToLower()}\r\n");
                await writer.FlushAsync();
                await writer.WriteAsync(prefix.Concat(header).ToArray());
                await writer.FlushAsync();
            };
        }
        else
        {
            // The response is "Trailers-Only". There are no gRPC messages in the response so the status
            // and other trailers can be placed in the header HEADERS frame
            return (s, w) =>
            {
                response.Headers[s] = w;
                return Task.CompletedTask;
            };
        }
    }

    public static AuthContext CreateAuthContext(X509Certificate2 clientCertificate)
    {
        // Map X509Certificate2 values to AuthContext. The name/values come BoringSSL via C Core
        // https://github.com/grpc/grpc/blob/a3cc5361e6f6eb679ccf5c36ecc6d0ca41b64f4f/src/core/lib/security/security_connector/ssl_utils.cc#L206-L248

        var properties = new Dictionary<string, List<AuthProperty>>(StringComparer.Ordinal);

        string? peerIdentityPropertyName = null;

        var dnsNames = X509CertificateHelpers.GetDnsFromExtensions(clientCertificate);
        foreach (var dnsName in dnsNames)
        {
            AddProperty(properties, GrpcProtocolConstants.X509SubjectAlternativeNameKey, dnsName);

            if (peerIdentityPropertyName == null)
            {
                peerIdentityPropertyName = GrpcProtocolConstants.X509SubjectAlternativeNameKey;
            }
        }

        var commonName = clientCertificate.GetNameInfo(X509NameType.SimpleName, false);
        if (commonName != null)
        {
            AddProperty(properties, GrpcProtocolConstants.X509CommonNameKey, commonName);
            if (peerIdentityPropertyName == null)
            {
                peerIdentityPropertyName = GrpcProtocolConstants.X509CommonNameKey;
            }
        }

        return new AuthContext(peerIdentityPropertyName, properties);

        static void AddProperty(Dictionary<string, List<AuthProperty>> properties, string name, string value)
        {
            ref var values = ref CollectionsMarshal.GetValueRefOrAddDefault(properties, name, out _);
            values ??= [];

            values.Add(AuthProperty.Create(name, Encoding.UTF8.GetBytes(value)));
        }
    }

    internal static bool CanWriteCompressed(WriteOptions? writeOptions)
    {
        if (writeOptions == null)
        {
            return true;
        }

        var canCompress = (writeOptions.Flags & WriteFlags.NoCompress) != WriteFlags.NoCompress;

        return canCompress;
    }

    internal static bool ShouldSkipHeader(string name)
    {
        return name.StartsWith(':') || GrpcProtocolConstants.FilteredHeaders.Contains(name);
    }
/*
    internal static IHttpListenerRequestLifetimeFeature GetRequestLifetimeFeature(HttpListenerContext HttpListenerContext)
    {
        var lifetimeFeature = HttpListenerContext.Features.Get<IHttpListenerRequestLifetimeFeature>();
        if (lifetimeFeature is null)
        {
            // This should only run in tests where the HttpListenerContext is manually created.
            lifetimeFeature = new HttpListenerRequestLifetimeFeature();
            HttpListenerContext.Features.Set(lifetimeFeature);
        }

        return lifetimeFeature;
    }
*/
}
