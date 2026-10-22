using MentalAccounting.Domain;
namespace MentalAccounting.Web;
public static class Labels
{
    public static string Movement(MovementKind value) => value switch { MovementKind.Deposit => "Versamento", MovementKind.Withdrawal => "Prelievo", _ => "Addebito" };
    public static string Basis(DayBasis value) => value switch { DayBasis.Actual360 => "Giorni effettivi / 360", DayBasis.ActualYear => "Giorni effettivi / anno effettivo", _ => "Giorni effettivi / 365" };
    public static string Timing(CreditTiming value) => value switch { CreditTiming.PeriodEnd => "Fine periodo", CreditTiming.NextMonthDay => "Giorno scelto del mese successivo", _ => "Primo giorno del mese successivo" };
}
