using Test;
using static Test.UnitTestMethods;
using Grpc.Core;

class TestMethodsImpl : Test.UnitTestMethods.UnitTestMethodsBase
{
      public override Task<Foo> Increment(Foo request, ServerCallContext context)
    {
        return Task.FromResult(new Test.Foo{Bar = request.Bar + 1});
    }

      public override async Task Range(Foo request, IServerStreamWriter<Foo> responseStream, ServerCallContext context)
      {
        for(int i=0; i<request.Bar; i++)
        {
          await Task.Delay(TimeSpan.FromSeconds(1));
          await responseStream.WriteAsync(new Foo {Bar = i});
        }
      }
}