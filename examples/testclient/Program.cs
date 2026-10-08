using System.Net;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Test;

var host = Environment.GetCommandLineArgs().Length > 1 ? Environment.GetCommandLineArgs()[1] : "localhost";
Console.WriteLine($"Connecting to {host}");
var channel = GrpcChannel.ForAddress($"http://{host}:5001", new GrpcChannelOptions
{
    HttpHandler = new GrpcWebHandler(new HttpClientHandler()),
    HttpVersion = HttpVersion.Version11
});
var client = new Test.UnitTestMethods.UnitTestMethodsClient(channel);
Console.WriteLine(client.Increment(new Foo{Bar = 41}));