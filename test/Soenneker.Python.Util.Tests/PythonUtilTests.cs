using Soenneker.Python.Util.Abstract;
using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Python.Util.Tests;

[Collection("Collection")]
public sealed class PythonUtilTests : FixturedUnitTest
{
    private readonly IPythonUtil _util;

    public PythonUtilTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<IPythonUtil>(true);
    }

    [Fact]
    public void Default()
    {

    }
}
