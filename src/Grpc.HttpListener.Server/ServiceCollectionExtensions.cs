using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Internal;
using Grpc.AspNetCore.Server.Model;
using Grpc.AspNetCore.Server.Model.Internal;
using Grpc.Shared.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grpc.HttpListener.Server;

public static class ServiceCollectionExtensions
{
    public static void AddGrpc(this IServiceCollection serviceCollection, string listenUrl)
    {
        serviceCollection.AddSingleton<InterceptorActivators>();
        serviceCollection.AddSingleton<ServiceMethodsRegistry>();
        serviceCollection.AddSingleton<LoggerFactory>();
        serviceCollection.AddSingleton(Options.Create(new GrpcServiceOptions()));
        serviceCollection.AddSingleton(new GrpcHttpListenerServerOptions(listenUrl));
        serviceCollection.AddHostedService<GrpcServerHttpListenerService>();
    }

    public static void MapGrpcService<T>(this IServiceCollection serviceCollection) where T : class
    {
        serviceCollection.AddTransient<IServiceMethodProvider<T>, BinderServiceMethodProvider<T>>();
        serviceCollection.AddTransient<IServiceMethodProvider<T>, ServiceDefinitionMethodProvider<T>>();
        serviceCollection.AddSingleton(Options.Create(new GrpcServiceOptions<T>()));
        serviceCollection.AddSingleton<IGrpcServiceActivator<T>, DefaultGrpcServiceActivator<T>>();
        serviceCollection.AddSingleton<ServerCallHandlerFactory<T>>();
        serviceCollection.AddSingleton<ServiceRouteBuilder<T>>();
        serviceCollection.AddHostedService<ServiceRouterBuilderService<T>>();
    }
}