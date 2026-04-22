using Soenneker.Python.Util.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.Python.Util.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class PythonUtilTests : HostedUnitTest
{
    private readonly IPythonUtil _util;

    public PythonUtilTests(Host host) : base(host)
    {
        _util = Resolve<IPythonUtil>(true);
    }

    [Test]
    public void Default()
    {

    }
}
