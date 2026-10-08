using System.Net;

namespace Grpc.HttpListener
{
    public delegate Task RequestDelegate(HttpListenerContext context);   
}