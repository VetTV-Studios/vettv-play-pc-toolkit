using VetTV.PlayPC.Core;
using Xunit;

namespace VetTV.PlayPC.Core.Tests;

public class ToolkitInfoTests
{
    [Fact]
    public void Disclaimer_MentionsNotAffiliated() =>
        Assert.Contains("Not affiliated with Google", ToolkitInfo.Disclaimer);
}
