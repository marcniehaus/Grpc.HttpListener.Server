using System.Net;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Test;

namespace testserver_maui;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		var channel = GrpcChannel.ForAddress("http://localhost:5001", new GrpcChannelOptions
		{
			HttpHandler = new GrpcWebHandler(new HttpClientHandler()),
			HttpVersion = HttpVersion.Version11
		});
		var client = new Test.UnitTestMethods.UnitTestMethodsClient(channel);
		var count = client.Increment(new Foo{Bar = 41}).Bar;
		CounterBtn.Text = $"gRPC returned {count}.";
		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}
