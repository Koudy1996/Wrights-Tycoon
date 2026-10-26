using System.Linq;
using UnityEngine;
using WrightTycoon.Core.Accounting;
using WrightTycoon.Core.Balance;

namespace WrightTycoon.Game.Launch;

public sealed partial class WrightTycoonLaunchRunner
{
    private bool TryQuoteFactoryFinancing(bool engine, FactoryBuildingSize size, out LoanOffer? offer, out string message)
    {
        offer = null;
        if (_accountingSystem is null || _productionFactory is null)
        { message = "Factory financing is not initialized."; return false; }
        var cost = FactoryBalance.GetConstructionCost(size) * (engine ? .5d : 1d);
        var assessment = _loanPortfolio.CalculateCreditAssessment(BuildLoanFinancialContext());
        if (!_loanPortfolio.TryBuildFactoryConstructionOffer(cost * .9d, assessment, out offer, out message) || offer is null)
            return false;
        var ownCash = cost - offer.Principal + offer.OriginationFee;
        if (_accountingSystem.Cash < ownCash)
        { message = "Additional cash needed for deposit and loan fee: " + FormatCurrency(ownCash - _accountingSystem.Cash); return false; }
        message = "90% construction financing is available.";
        return true;
    }

    private void DrawFactoryFinancing(bool engine, FactoryBuildingSize size, string buildKey)
    {
        var available = TryQuoteFactoryFinancing(engine, size, out var offer, out var message);
        var enabled = GUI.enabled;
        GUI.enabled = enabled && available;
        if (PlannerButton("finance." + buildKey.Substring("build.".Length), "FINANCOVAT — 90 %", GUILayout.Height(34f)))
            StartFinancedFactoryConstruction(engine, size);
        GUI.enabled = enabled;
        if (offer is not null)
        {
            var cost = FactoryBalance.GetConstructionCost(size) * (engine ? .5d : 1d);
            DrawWrappedFactoryStatus("Deposit 10%: " + FormatCurrency(cost - offer.Principal) + " · fee: " + FormatCurrency(offer.OriginationFee));
            DrawWrappedFactoryStatus("Loan: " + FormatCurrency(offer.Principal) + " · 10 years · APR " + FormatUiNumber(offer.AnnualInterestRate * 100, "0.00") + "% · " + FormatCurrency(offer.ScheduledMonthlyPayment) + "/month · 2 pp below standard");
            DrawWrappedFactoryStatus("First payment: " + FormatSimulationDate(_currentDate.AddMonths(FactoryBalance.GetConstructionMonths(size))) + "; no installments or accrued interest during construction.");
        }
        if (!available) DrawWrappedFactoryStatus(message);
    }

    private bool StartFinancedFactoryConstruction(bool engine, FactoryBuildingSize size)
    {
        // Re-quote at click time. No loan, payment or plant is created if eligibility fails.
        if (!TryQuoteFactoryFinancing(engine, size, out var offer, out var message) || offer is null)
        { _factoryActionMessage = message; return false; }
        var availableCash = _accountingSystem!.Cash + offer.Principal - offer.OriginationFee;
        double cost;
        var started = engine
            ? _engineManufacturing.TryConstruct(size, availableCash, out cost, out message)
            : _productionFactory!.TryStartFactoryConstruction(size, availableCash, out cost, out message);
        if (!started) { _factoryActionMessage = message; return false; }
        var loan = _loanPortfolio.AcceptFactoryConstructionOffer(offer, _elapsedMonths, FactoryBalance.GetConstructionMonths(size));
        _accountingSystem.RegisterLoanDisbursement(_elapsedMonths, _currentDate, offer.Principal, loan.LoanId);
        _accountingSystem.RegisterLoanFee(_elapsedMonths, _currentDate, offer.OriginationFee, loan.LoanId, "Factory financing origination fee.");
        _accountingSystem.RegisterCapitalExpense(_elapsedMonths, _currentDate, cost, size + (engine ? " engine" : " aircraft") + " factory construction (90% financed).");
        _factoryPlannerTab = engine ? 1 : 0;
        _factoryPlannerSelection = engine ? "engine:" + _engineManufacturing.Sites.Last().SiteId : "aircraft:" + _productionFactory!.Factories.Last().FactoryId;
        _factoryPlannerStatus = 0;
        _factoryPlannerRefreshAt = 0;
        _showFactoryPlannerConstruction = false;
        _factoryActionMessage = message + " Loan " + loan.LoanId + ": " + FormatCurrency(offer.Principal) + ".";
        return true;
    }
}
