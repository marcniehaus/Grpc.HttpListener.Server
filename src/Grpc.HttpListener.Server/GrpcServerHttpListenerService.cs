using System.Net;
using System.Linq;
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
        this._serviceMethodRegistry = serviceMethodsRegistry;
        this._serverOptions = serverOptions;
        this._listener = new System.Net.HttpListener();
        this._listener.Prefixes.Add(this._serverOptions.ListenUrl);
        this._logger = loggerFactory.CreateLogger<GrpcServerHttpListenerService>();
    }
    
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        this._listener.Start();
        this._listenerTask = Task.Run(Listen);
        return Task.CompletedTask;
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        this._cancellationTokenSource.Cancel();
        this._listener.Close();
        await (this._listenerTask ?? Task.CompletedTask);
    }

        private async Task Listen()
    {
        //TOOD: Use logger instead of Console.WriteLine in this method

        try
        {
            while(!this._cancellationTokenSource.IsCancellationRequested)
            {
                HttpListenerContext context = await this._listener.GetContextAsync();
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
        catch(Exception) when (this._cancellationTokenSource.IsCancellationRequested)
        {            
        }
        catch(Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error while listening: {ex}.");
        }
    }

    private async Task HandleHttpCall(HttpListenerContext context)
    {
        var methods = this._serviceMethodRegistry.Methods.ToDictionary(m => m.Pattern, m => m);

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