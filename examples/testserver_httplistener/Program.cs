using Grpc.HttpListener.Server;
using Microsoft.Extensions.Hosting;

var listenUrl = "http://+:5001/";

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddGrpc(listenUrl);
builder.Services.MapGrpcService<TestService>();
IHost host = builder.Build();
host.Run();

System.Console.WriteLine($"Server @ {listenUrl} started - press enter to exit.");
System.Console.ReadLine();