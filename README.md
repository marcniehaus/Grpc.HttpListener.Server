# gRPC server implemented using HttpListener

Due to `Microsoft.NET.Sdk.Web` projects not being compatible to MAUI Android it
is currently difficult to host gRPC services in MAUI Android applications.
The issue addressing this problem is still open:
https://github.com/dotnet/aspnetcore/issues/35077

To be able to host gRPC in MAUI Android demo applications this fork was
developed. It can be thought of as proof-of-concept stating how to replace
ASP.NET in the gRPC server code by another web server
(HttpListener in this case).
Please note that Microsoft discourages using HttpListener in new projects:
https://learn.microsoft.com/de-de/dotnet/api/system.net.httplistener?view=net-10.0
Therefore, this fork should not be used in productive code!
Please use the official [grpc-dotnet](https://github.com/grpc/grpc-dotnet)
code for productive purposes.

In order to see how to use this code, please have a look into the
[testserver_httplistener example](examples/testserver_httplistener/Program.cs).