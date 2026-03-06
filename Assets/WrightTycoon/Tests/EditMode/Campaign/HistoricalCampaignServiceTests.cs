using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Balance;
using WrightTycoon.Core.Campaign;
using WrightTycoon.Core.Categories;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Campaign;

[TestFixture]
public sealed class HistoricalCampaignServiceTests
{
    [Test]
    public void Repository_RejectsArticlesThatExceedTheWordLimit()
    {
        var article = string.Join(" ", Enumerable.Repeat("word", HistoricalCampaignValidator.ArticleWordLimit + 1));
        var json = WrapEvents(
            CreateEventJson(
                "evt.long_article",
                "Long Article",
                year: 2001,
                month: 9,
                article));

        var repository = new HistoricalCampaignJsonRepository();

        var exception = Assert.Throws<InvalidOperationException>(() => repository.LoadFromJson(json));
        Assert.That(exception!.Message, Does.Contain("limit"));
    }

    [Test]
    public void Service_UsesTheFirstEventWhenTwoDefinitionsShareTheSameMonth()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson("evt.primary", "Primary Event", 2001, 9, "Primary article text."),
            CreateEventJson("evt.duplicate", "Duplicate Event", 2001, 9, "Duplicate article text."));

        var service = repository.LoadFromJson(json);
        var state = new HistoricalCampaignState();
        var result = service.TryTrigger(new SimulationDate(2001, 9), state);

        Assert.That(service.LoadWarnings, Has.Count.EqualTo(1));
        Assert.That(result.HasTriggeredEvent, Is.True);
        Assert.That(result.TriggeredEvent!.Id, Is.EqualTo("evt.primary"));
        Assert.That(state.FiredEventIds, Is.EquivalentTo(new[] { "evt.primary" }));
    }

    [Test]
    public void Service_DoesNotRefireTheSameMonthOnceTheSelectedEventHasTriggered()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson("evt.first", "First Event", 2001, 9, "First article text."),
            CreateEventJson("evt.second", "Second Event", 2002, 1, "Second article text."));

        var service = repository.LoadFromJson(json);
        var state = new HistoricalCampaignState();

        var firstPass = service.TryTrigger(new SimulationDate(2001, 9), state);
        var secondPass = service.TryTrigger(new SimulationDate(2001, 9), state);
        var nextEvent = service.GetNextUnfiredEvent(state);

        Assert.That(firstPass.HasTriggeredEvent, Is.True);
        Assert.That(secondPass.HasTriggeredEvent, Is.False);
        Assert.That(nextEvent, Is.Not.Null);
        Assert.That(nextEvent!.Id, Is.EqualTo("evt.second"));
    }

    [Test]
    public void Service_ReturnsTheNextUpcomingEventForTheCurrentDate()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson("evt.past", "Past Event", 1947, 10, "Past article text."),
            CreateEventJson("evt.future", "Future Event", 2001, 9, "Future article text."));

        var service = repository.LoadFromJson(json);
        var state = new HistoricalCampaignState();

        var nextUpcoming = service.GetNextUpcomingUnfiredEvent(new SimulationDate(2000, 1), state);

        Assert.That(nextUpcoming, Is.Not.Null);
        Assert.That(nextUpcoming!.Id, Is.EqualTo("evt.future"));
    }

    [Test]
    public void Service_BuildsActiveContractModifiersFromFiredEvents()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.korea",
                "Korea",
                1950,
                6,
                "Article text.",
                "contract_mix_modifier",
                "military",
                "contract.event.korea_war.mix",
                "contract_price_modifier",
                "military",
                "contract.event.korea_war.price"),
            CreateEventJson(
                "evt.oil",
                "Oil",
                1973,
                10,
                "Article text.",
                "contract_price_modifier",
                "civil",
                "contract.event.oil_crisis.price"));

        var service = repository.LoadFromJson(json);
        var state = new HistoricalCampaignState();
        Assert.That(service.TryTrigger(new SimulationDate(1950, 6), state).HasTriggeredEvent, Is.True);
        Assert.That(service.TryTrigger(new SimulationDate(1973, 10), state).HasTriggeredEvent, Is.True);

        var modifiers = service.BuildActiveContractModifiers(state);

        Assert.That(modifiers.MilitaryCategoryWeightMultiplier, Is.EqualTo(ContractBalance.CampaignMixKoreaWar).Within(1e-9));
        Assert.That(modifiers.MilitaryPriceMultiplier, Is.EqualTo(ContractBalance.CampaignPriceKoreaWar).Within(1e-9));
        Assert.That(modifiers.CivilPriceMultiplier, Is.EqualTo(ContractBalance.CampaignPriceGlobalOilCrisis).Within(1e-9));
        Assert.That(modifiers.HasCategoryMixAdjustment, Is.True);
    }

    [Test]
    public void Service_KeepsWideBodyLockedUntilUnlockEventFires()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.wide_body_unlock",
                "Wide Body",
                1960,
                1,
                "Article text.",
                "unlock_category",
                "HeavyTransportWideBody",
                ""));

        var service = repository.LoadFromJson(json);
        var state = new HistoricalCampaignState();

        var beforeUnlock = service.BuildActiveContractModifiers(state);

        Assert.That(beforeUnlock.IsCategoryUnlocked(AircraftCategory.HeavyTransportWideBody), Is.False);
        Assert.That(beforeUnlock.IsCategoryUnlocked(AircraftCategory.RegionalJet), Is.True);

        Assert.That(service.TryTrigger(new SimulationDate(1960, 1), state).HasTriggeredEvent, Is.True);
        var afterUnlock = service.BuildActiveContractModifiers(state);
        Assert.That(afterUnlock.IsCategoryUnlocked(AircraftCategory.HeavyTransportWideBody), Is.True);
    }

    [Test]
    public void Service_UnlocksFuelConsumptionContractParameter_WhenUnlockEventFires()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.oil_unlock",
                "Oil Unlock",
                1973,
                10,
                "Article text.",
                "unlock_contract_parameter",
                "fuel_consumption",
                ""));

        var service = repository.LoadFromJson(json);
        var state = new HistoricalCampaignState();

        var beforeUnlock = service.BuildActiveContractModifiers(state);
        Assert.That(
            beforeUnlock.IsContractParameterUnlocked(ContractBalance.HistoricalCampaignContractParameterFuelConsumption),
            Is.False);

        Assert.That(service.TryTrigger(new SimulationDate(1973, 10), state).HasTriggeredEvent, Is.True);

        var afterUnlock = service.BuildActiveContractModifiers(state);
        Assert.That(
            afterUnlock.IsContractParameterUnlocked(ContractBalance.HistoricalCampaignContractParameterFuelConsumption),
            Is.True);
    }

    [Test]
    public void Repository_RejectsUnknownContractParameterUnlockTargets()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.invalid_unlock",
                "Invalid Unlock",
                1973,
                10,
                "Article text.",
                "unlock_contract_parameter",
                "unknown_parameter",
                ""));

        var exception = Assert.Throws<InvalidOperationException>(() => repository.LoadFromJson(json));
        Assert.That(exception!.Message, Does.Contain("unknown parameter"));
    }

    [Test]
    public void Repository_RejectsUnsupportedEffectTypes()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.unsupported_effect",
                "Unsupported Effect",
                1973,
                10,
                "Article text.",
                "unsupported_effect",
                "civil",
                "contract.event.oil_crisis.price"));

        var exception = Assert.Throws<InvalidOperationException>(() => repository.LoadFromJson(json));
        Assert.That(exception!.Message, Does.Contain("unsupported type"));
    }

    [Test]
    public void Repository_RejectsContractMixEffectWithoutBalanceKey()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.missing_mix_balance",
                "Missing Mix Balance",
                1973,
                10,
                "Article text.",
                "contract_mix_modifier",
                "civil",
                ""));

        var exception = Assert.Throws<InvalidOperationException>(() => repository.LoadFromJson(json));
        Assert.That(exception!.Message, Does.Contain("balanceKey"));
    }

    [Test]
    public void Repository_RejectsContractMixEffectWithInvalidTarget()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.invalid_mix_target",
                "Invalid Mix Target",
                1973,
                10,
                "Article text.",
                "contract_mix_modifier",
                "cargo",
                "contract.event.global_air_travel.mix"));

        var exception = Assert.Throws<InvalidOperationException>(() => repository.LoadFromJson(json));
        Assert.That(exception!.Message, Does.Contain("must be 'civil' or 'military'"));
    }

    [Test]
    public void Repository_RejectsEventsOutsideSupportedYearRange()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.out_of_range_year",
                "Out Of Range",
                2025,
                1,
                "Article text.",
                "technology_shift",
                "avionics",
                ""));

        var exception = Assert.Throws<InvalidOperationException>(() => repository.LoadFromJson(json));
        Assert.That(exception!.Message, Does.Contain("range"));
    }

    [Test]
    public void Repository_AcceptsTechnologyShiftEffectsWithTarget()
    {
        var repository = new HistoricalCampaignJsonRepository();
        var json = WrapEvents(
            CreateEventJson(
                "evt.technology_shift",
                "Technology Shift",
                1995,
                1,
                "Article text.",
                "technology_shift",
                "avionics",
                ""));

        var service = repository.LoadFromJson(json);
        Assert.That(service.ScheduledEvents, Has.Count.EqualTo(1));
        Assert.That(service.ScheduledEvents[0].Effects[0].Type, Is.EqualTo("technology_shift"));
    }

    private static string WrapEvents(params string[] eventJsonBlocks) =>
        "{\n" +
        "  \"events\": [\n" +
        string.Join(",\n", eventJsonBlocks) +
        "\n  ]\n" +
        "}";

    private static string CreateEventJson(
        string id,
        string title,
        int year,
        int month,
        string articleText,
        params string[] effects) =>
        "    {\n" +
        $"      \"id\": \"{id}\",\n" +
        $"      \"title\": \"{title}\",\n" +
        $"      \"year\": {year},\n" +
        $"      \"month\": {month},\n" +
        $"      \"articleText\": \"{articleText}\",\n" +
        "      \"effects\": [\n" +
        BuildEffectsJson(effects) +
        "\n      ]\n" +
        "    }";

    private static string BuildEffectsJson(string[] effects)
    {
        if (effects is null || effects.Length == 0)
        {
            return "        {\n" +
                   "          \"type\": \"test_marker\"\n" +
                   "        }";
        }

        if (effects.Length % 3 != 0)
        {
            throw new ArgumentException("Effects must be supplied as type/target/balanceKey triplets.", nameof(effects));
        }

        var blocks = Enumerable.Range(0, effects.Length / 3)
            .Select(index =>
            {
                var offset = index * 3;
                var type = effects[offset];
                var target = effects[offset + 1];
                var balanceKey = effects[offset + 2];
                return "        {\n" +
                       $"          \"type\": \"{type}\",\n" +
                       $"          \"target\": \"{target}\",\n" +
                       $"          \"balanceKey\": \"{balanceKey}\"\n" +
                       "        }";
            });

        return string.Join(",\n", blocks);
    }
}
