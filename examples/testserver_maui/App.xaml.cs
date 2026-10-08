using Grpc.HttpListener.Server;
using Microsoft.Extensions.Hosting;

namespace testserver_maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		Task.Run(() =>
		{
			var listenUrl = "http://+:5001/";
			HostApplicationBuilder builder = Host.CreateApplicationBuilder();
			builder.Services.AddGrpc(listenUrl);
			builder.Services.MapGrpcService<TestService>();
			IHost host = builder.Build();
			host.Run();
		});
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}