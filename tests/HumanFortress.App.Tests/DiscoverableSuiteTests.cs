using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

[TestClass]
[TestCategory("discoverable")]
public sealed class DiscoverableSuiteTests
{
    [TestMethod]
    [TestCategory(TestSuiteCatalog.TransportConstructionCraft)]
    public void TransportConstructionCraft()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.TransportConstructionCraft);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.MiningItemsDiff)]
    public void MiningItemsDiff()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.MiningItemsDiff);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.NavigationPath)]
    public void NavigationPath()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.NavigationPath);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.ViewportGeometry)]
    public void ViewportGeometry()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.ViewportGeometry);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.RuntimeCheckpoint)]
    public void RuntimeCheckpoint()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.RuntimeCheckpoint);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.TickSchedulerLifecycle)]
    public void TickSchedulerLifecycle()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.TickSchedulerLifecycle);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.ContentIdentity)]
    public void ContentIdentity()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.ContentIdentity);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.CoreRuntime)]
    public void CoreRuntime()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.CoreRuntime);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.ArchitectureBoundary)]
    public void ArchitectureBoundary()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.ArchitectureBoundary);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.DeterministicAuthority)]
    public void DeterministicAuthority()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.DeterministicAuthority);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.WorldGenEvidence)]
    public void WorldGenEvidence()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.WorldGenEvidence);
    }

    [TestMethod]
    [TestCategory(TestSuiteCatalog.PhaseValidation)]
    public void PhaseValidation()
    {
        LegacySmokeSuiteRunner.RunSuite(TestSuiteCatalog.PhaseValidation);
    }
}

[TestClass]
[TestCategory("end-to-end")]
public sealed class EndToEndSmokeTests
{
    [TestMethod]
    public void AllSuitesCompleteInCanonicalOrder()
    {
        LegacySmokeSuiteRunner.RunAll();
    }
}
