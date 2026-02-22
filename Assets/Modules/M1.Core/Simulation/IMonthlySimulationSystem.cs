namespace WrightTycoon.Core.Simulation;

public interface IMonthlySimulationSystem<TState>
    where TState : class
{
    string SystemId { get; }

    int ExecutionOrder { get; }

    void Execute(MonthlySimulationContext<TState> context);
}
