
using Grpc.AspNetCore.Server.Model.Internal;
using Microsoft.Extensions.Hosting;

namespace Grpc.HttpListener.Server;

internal class ServiceRouterBuilderService<T> : IHostedService where T : class
{
    private ServiceRouteBuilder<T> _serviceRouteBuilder;

    public ServiceRouterBuilderService(ServiceRouteBuilder<T> serviceRouteBuilder)
    {
        _serviceRouteBuilder = serviceRouteBuilder;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this._serviceRouteBuilder.Build();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
