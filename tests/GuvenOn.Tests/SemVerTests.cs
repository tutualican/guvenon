using GuvenOn.Core;

namespace GuvenOn.Tests;

[TestClass]
public sealed class SemVerTests
{
    [TestMethod]
    [DataRow("1.0.0", true)]
    [DataRow("v1.2.3", true)]
    [DataRow("V2.0.0", true)]
    [DataRow("1.2.3-alpha", true)]
    [DataRow("1.2.3-alpha.1", true)]
    [DataRow("1.2.3-0.3.7", true)]
    [DataRow("1.2.3+build.5", true)]
    [DataRow("1.2.3-alpha+build.5", true)]
    [DataRow("1.2.3-x-y-z.1", true)]
    [DataRow("01.2.3", false)]
    [DataRow("1.02.3", false)]
    [DataRow("1.2.03", false)]
    [DataRow("1.2.3-", false)]
    [DataRow("1.2.3+", false)]
    [DataRow("1.2.3-alpha..1", false)]
    [DataRow("1.2.3-alpha.01", false)]
    [DataRow("1.2.3-alpha_1", false)]
    [DataRow("1.2", false)]
    [DataRow("1.2.3.4", false)]
    [DataRow("1.2.3-ä", false)]
    [DataRow("1.2.3+meta_1", false)]
    [DataRow("v", false)]
    [DataRow("", false)]
    [DataRow(" 1.2.3 ", true)]
    public void TryParse_ValidatesSemVer(string value, bool expected)
    {
        Assert.AreEqual(expected, SemVer.TryParse(value, out _));
    }

    [TestMethod]
    [DataRow("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [DataRow("1.0.0-alpha.1", "1.0.0-alpha.beta", -1)]
    [DataRow("1.0.0-alpha.beta", "1.0.0-beta", -1)]
    [DataRow("1.0.0-beta", "1.0.0-beta.2", -1)]
    [DataRow("1.0.0-beta.2", "1.0.0-beta.11", -1)]
    [DataRow("1.0.0-beta.11", "1.0.0-rc.1", -1)]
    [DataRow("1.0.0-rc.1", "1.0.0", -1)]
    [DataRow("1.0.0", "1.0.0+build", 0)]
    [DataRow("1.0.0-2", "1.0.0-10", -1)]
    [DataRow("1.0.0-99999999999999999999", "1.0.0-100000000000000000000", -1)]
    public void CompareTo_UsesSemVerOrdering(string left, string right, int expectedSign)
    {
        Assert.IsTrue(SemVer.TryParse(left, out var a));
        Assert.IsTrue(SemVer.TryParse(right, out var b));
        Assert.AreEqual(expectedSign, Math.Sign(a.CompareTo(b)));
    }

    [TestMethod]
    public void BuildMetadata_IsIgnored()
    {
        Assert.IsTrue(SemVer.TryParse("1.2.3+one", out var a));
        Assert.IsTrue(SemVer.TryParse("1.2.3+two", out var b));
        Assert.AreEqual(a, b);
        Assert.AreEqual("1.2.3", a.ToString());
    }
}
