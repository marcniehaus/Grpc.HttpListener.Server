namespace Grpc.HttpListener.Server.Tests;

using Grpc.HttpListener.Server;
using Microsoft.Extensions.Hosting;
using System.Net;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Test;
using System.Reflection;

public class GrpcHttpListenerServerTests
{
    [Fact]
    public async Task Unary()
    {
        var listenUrl = "http://localhost:5001/";

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGrpc(listenUrl);
        builder.Services.MapGrpcService<TestMethodsImpl>();
        var host = builder.Build();
        try
        {
            await host.StartAsync(CancellationToken.None);

            var channel = GrpcChannel.ForAddress("http://localhost:5001", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler()),
                HttpVersion = HttpVersion.Version11
            });
            var client = new Test.UnitTestMethods.UnitTestMethodsClient(channel);
            Assert.Equal(42, client.Increment(new Foo{Bar = 41}).Bar);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }

    }

    [Fact]

    public async Task Streaming()
    {
        var listenUrl = "http://localhost:5001/";

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGrpc(listenUrl);
        builder.Services.MapGrpcService<TestMethodsImpl>();
        using var host = builder.Build();
        try
        {
            await host.StartAsync(CancellationToken.None);

            var channel = GrpcChannel.ForAddress("http://localhost:5001", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler()),
                HttpVersion = HttpVersion.Version11
            });
            var client = new Test.UnitTestMethods.UnitTestMethodsClient(channel);
            List<int> results = new();
            var range = client.Range(new Foo{Bar = 5});
            while(await range.ResponseStream.MoveNext(CancellationToken.None))
            {
                results.Add(range.ResponseStream.Current.Bar);
            }
            Assert.Equal(5, results.Count);
            Assert.Equal(0, results[0]);
            Assert.Equal(1, results[1]);
            Assert.Equal(2, results[2]);
            Assert.Equal(3, results[3]);
            Assert.Equal(4, results[4]);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }

    }
}
