using System;
using System.Linq;
using System.Reflection;
using Thaivia.Core.Simulation.Economy;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

public class MoneyLedgerTests
{
    [Fact]
    public void EveryPublicMoneyMember_UsesIntegerTypesOnly()
    {
        // Structural proof for "money is integer, no floats anywhere in
        // the ledger": reflect over every public property/method on
        // MoneyLedger and assert none has a float/double/decimal
        // parameter or return type.
        var type = typeof(MoneyLedger);
        var badTypes = new[] { typeof(float), typeof(double), typeof(decimal) };

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.DoesNotContain(prop.PropertyType, badTypes);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.IsSpecialName))
        {
            Assert.DoesNotContain(method.ReturnType, badTypes);
            foreach (var p in method.GetParameters())
            {
                Assert.DoesNotContain(p.ParameterType, badTypes);
            }
        }
    }

    [Fact]
    public void InitialCash_IsAvailableAndUnreserved()
    {
        var ledger = new MoneyLedger(1000);
        Assert.Equal(1000, ledger.Cash);
        Assert.Equal(0, ledger.Reserved);
        Assert.Equal(1000, ledger.Available);
    }

    [Fact]
    public void Reserve_MovesFromAvailableToReserved_WithoutTouchingCash()
    {
        var ledger = new MoneyLedger(1000);
        ledger.Reserve(LedgerAccountKind.Capex, 400);

        Assert.Equal(1000, ledger.Cash); // cash untouched by reservation.
        Assert.Equal(400, ledger.Reserved);
        Assert.Equal(600, ledger.Available);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_Throws()
    {
        var ledger = new MoneyLedger(100);
        Assert.Throws<InvalidOperationException>(() => ledger.Reserve(LedgerAccountKind.Capex, 101));
        Assert.Equal(100, ledger.Available); // rejected reservation changes nothing.
    }

    [Fact]
    public void ChargeFromReservation_DeductsCashAndReservationTogether_ExactlyOnce()
    {
        var ledger = new MoneyLedger(1000);
        ledger.Reserve(LedgerAccountKind.Capex, 400);
        ledger.ChargeFromReservation(LedgerAccountKind.Capex, 400);

        Assert.Equal(600, ledger.Cash);
        Assert.Equal(0, ledger.Reserved);
        Assert.Equal(600, ledger.Available);
    }

    [Fact]
    public void ChargeFromReservation_MoreThanReserved_Throws()
    {
        var ledger = new MoneyLedger(1000);
        ledger.Reserve(LedgerAccountKind.Capex, 100);
        Assert.Throws<InvalidOperationException>(() => ledger.ChargeFromReservation(LedgerAccountKind.Capex, 101));
    }

    [Fact]
    public void ChargeDirect_DeductsCashWithoutAnyReservation()
    {
        var ledger = new MoneyLedger(1000);
        ledger.ChargeDirect(LedgerAccountKind.Opex, 250);
        Assert.Equal(750, ledger.Cash);
        Assert.Equal(0, ledger.Reserved);
    }

    [Fact]
    public void RestoreRoundTrip_ReproducesIdenticalBalancesPerKind()
    {
        var ledger = new MoneyLedger(1_000_000);
        ledger.Reserve(LedgerAccountKind.Capex, 200_000);
        ledger.Reserve(LedgerAccountKind.Opex, 50_000);
        ledger.ChargeFromReservation(LedgerAccountKind.Opex, 20_000);

        var cash = ledger.CaptureCashByKind();
        var reserved = ledger.CaptureReservedByKind();
        var restored = MoneyLedger.Restore(cash, reserved);

        Assert.Equal(ledger.Cash, restored.Cash);
        Assert.Equal(ledger.Reserved, restored.Reserved);
        foreach (LedgerAccountKind kind in Enum.GetValues(typeof(LedgerAccountKind)))
        {
            Assert.Equal(ledger.CashOf(kind), restored.CashOf(kind));
            Assert.Equal(ledger.ReservedOf(kind), restored.ReservedOf(kind));
        }
    }
}
