using Test;
using Grpc.Core;

public class TestService : Test.UnitTestMethods.UnitTestMethodsBase
{
      public override Task<Foo> Increment(Foo request, ServerCallContext context)
    {
        return Task.FromResult(new Foo{ Bar = request.Bar + 1 });
    }
}