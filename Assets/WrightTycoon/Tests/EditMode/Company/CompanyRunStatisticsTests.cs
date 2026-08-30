using NUnit.Framework;
using WrightTycoon.Core.Company;

namespace WrightTycoon.Core.Tests.Company;

[TestFixture]
public sealed class CompanyRunStatisticsTests
{
    [Test]
    public void Bankruptcy_TriggersOnlyBelowZero()
    {
        Assert.That(BankruptcyPolicy.IsBankrupt(1d), Is.False);
        Assert.That(BankruptcyPolicy.IsBankrupt(0d), Is.False);
        Assert.That(BankruptcyPolicy.IsBankrupt(-0.01d), Is.True);
    }

    [Test]
    public void LifetimeStatistics_CountUniqueTypesVariantsProductionAndSales()
    {
        var statistics = new CompanyRunStatistics();
        statistics.RegisterDevelopedAircraftType("type.alpha", isVariant: false);
        statistics.RegisterDevelopedAircraftType("type.alpha", isVariant: false);
        statistics.RegisterDevelopedAircraftType("type.alpha.v2", isVariant: true);
        statistics.RegisterAircraftProduced(18);
        statistics.RegisterAircraftProduced(7);
        statistics.RegisterAircraftSold(11);

        Assert.That(statistics.DevelopedAircraftTypes, Is.EqualTo(1));
        Assert.That(statistics.DevelopedAircraftVariants, Is.EqualTo(1));
        Assert.That(statistics.TotalAircraftVariants, Is.EqualTo(2));
        Assert.That(statistics.TotalAircraftProduced, Is.EqualTo(25));
        Assert.That(statistics.TotalAircraftSold, Is.EqualTo(11));
    }

    [Test]
    public void SnapshotRoundTrip_PreservesFinalRunStatisticsAndVariantClassification()
    {
        var source = new CompanyRunStatistics();
        source.RegisterDevelopedAircraftType("type.alpha", isVariant: false);
        source.RegisterDevelopedAircraftType("type.alpha", isVariant: true);
        source.RegisterDevelopedAircraftType("type.beta", isVariant: false);
        source.RegisterAircraftProduced(42);
        source.RegisterAircraftSold(30);

        var restored = new CompanyRunStatistics();
        restored.RestoreSnapshot(source.CreateSnapshot());

        Assert.That(restored.DevelopedAircraftTypes, Is.EqualTo(1));
        Assert.That(restored.DevelopedAircraftVariants, Is.EqualTo(1));
        Assert.That(restored.TotalAircraftVariants, Is.EqualTo(2));
        Assert.That(restored.TotalAircraftProduced, Is.EqualTo(42));
        Assert.That(restored.TotalAircraftSold, Is.EqualTo(30));
    }
}
