using DestinyChatDesktop.Embedded;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DestinyChatDesktop.Tests;

[TestClass]
public sealed class SmokeTests
{
    [TestMethod]
    public void ClampZoom_zero_returns_one()
    {
        Assert.AreEqual(1.0, MainForm.ClampZoom(0));
    }

    [TestMethod]
    public void ClampZoom_within_range_roundtrips()
    {
        Assert.AreEqual(1.2, MainForm.ClampZoom(1.2));
    }
}
