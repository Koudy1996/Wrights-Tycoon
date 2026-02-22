using NUnit.Framework;
using WrightTycoon.Core.Simulation;

namespace WrightTycoon.Core.Tests.Simulation;

[TestFixture]
public sealed class MonthlySimulationEngineTests
{
    [Test]
    public void Run_ExecutesSystemsInOrderAndAdvancesCalendar()
    {
        var state = new FinanceState { Cash = 100d };
        var systems = new IMonthlySimulationSystem<FinanceState>[]
        {
            new ExpensesSystem(),
            new IncomeSystem(),
        };

        var engine = new MonthlySimulationEngine<FinanceState>(systems);
        var result = engine.Run(state, new SimulationDate(1930, 1), monthCount: 2);

        Assert.That(result.FinalState.Cash, Is.EqualTo(240d).Within(1e-9));
        Assert.That(result.StartDate, Is.EqualTo(new SimulationDate(1930, 1)));
        Assert.That(result.EndDate, Is.EqualTo(new SimulationDate(1930, 3)));
        Assert.That(result.MonthReports.Count, Is.EqualTo(2));
        Assert.That(result.MonthReports[0].ExecutedSystemIds, Is.EqualTo(new[] { "finance.expenses", "finance.income" }));
        Assert.That(result.MonthReports[0].Events, Is.EqualTo(new[] { "Expenses -30", "Income +100" }));
    }

    private sealed class FinanceState
    {
        public double Cash { get; set; }
    }

    private sealed class ExpensesSystem : IMonthlySimulationSystem<FinanceState>
    {
        public string SystemId => "finance.expenses";

        public int ExecutionOrder => 10;

        public void Execute(MonthlySimulationContext<FinanceState> context)
        {
            context.State.Cash -= 30d;
            context.AddEvent("Expenses -30");
        }
    }

    private sealed class IncomeSystem : IMonthlySimulationSystem<FinanceState>
    {
        public string SystemId => "finance.income";

        public int ExecutionOrder => 20;

        public void Execute(MonthlySimulationContext<FinanceState> context)
        {
            context.State.Cash += 100d;
            context.AddEvent("Income +100");
        }
    }
}
