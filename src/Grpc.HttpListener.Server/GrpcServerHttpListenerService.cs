using System.Net;
using Grpc.AspNetCore.Server.Model.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Grpc.HttpListener.Server;

public class GrpcServerHttpListenerService : IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ServiceMethodsRegistry _serviceMethodRegistry;
    private readonly GrpcHttpListenerServerOptions _serverOptions;
    private readonly System.Net.HttpListener _listener;
    private Task? _listenerTask;
    private ILogger _logger;

    public GrpcServerHttpListenerService(ServiceMethodsRegistry serviceMethodsRegistry, GrpcHttpListenerServerOptions serverOptions, LoggerFactory loggerFactory)
    {
        _serviceMethodRegistry = serviceMethodsRegistry;
        _serverOptions = serverOptions;
        _listener = new System.Net.HttpListener();
        _listener.Prefixes.Add(_serverOptions.ListenUrl);
        _logger = loggerFactory.CreateLogger<GrpcServerHttpListenerService>();
    }
    
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _listenerTask = Task.Run(Listen);
        return Task.CompletedTask;
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        _listener.Close();
        await (_listenerTask ?? Task.CompletedTask);
    }

        private async Task Listen()
    {
        //TOOD: Use logger instead of Console.WriteLine in this method

        try
        {
            while(!_cancellationTokenSource.IsCancellationRequested)
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                _logger.Log(LogLevel.Information, $"Got a request for '{context.Request.Url}'");
                _ = Task.Run((async () =>
                {
                   try
                    {
                        await HandleHttpCall(context);
                    } 
                    catch(Exception ex)
                    {
                        _logger.Log(LogLevel.Error, $"Error while handling request: {ex}.");
                    }
                }));
            }
        }
        catch(Exception) when (_cancellationTokenSource.IsCancellationRequested)
        {            
        }
        catch(Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error while listening: {ex}.");
        }
    }

    private async Task HandleHttpCall(HttpListenerContext context)
    {
        var methods = _serviceMethodRegistry.Methods.ToDictionary(m => m.Pattern, m => m);

        if(!methods.TryGetValue(context.Request.Url.AbsolutePath, out var method)) //TODO: Is this the correct way to get the URL?
        {
            _logger.Log(LogLevel.Error, $"Unable to handle {context.Request.Url}");
            return;
        }

        context.Response.SendChunked = true;        
        
        await method.RequestDelegate(context);

        context.Response.OutputStream.Close();
        context.Response.Close();
    }
}