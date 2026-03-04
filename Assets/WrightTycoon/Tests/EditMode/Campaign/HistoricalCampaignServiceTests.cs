using System;
using System.Linq;
using NUnit.Framework;
using WrightTycoon.Core.Campaign;
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
        string articleText) =>
        "    {\n" +
        $"      \"id\": \"{id}\",\n" +
        $"      \"title\": \"{title}\",\n" +
        $"      \"year\": {year},\n" +
        $"      \"month\": {month},\n" +
        $"      \"articleText\": \"{articleText}\",\n" +
        "      \"effects\": [\n" +
        "        {\n" +
        "          \"type\": \"test_marker\"\n" +
        "        }\n" +
        "      ]\n" +
        "    }";
}
