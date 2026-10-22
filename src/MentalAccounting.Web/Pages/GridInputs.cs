using System.ComponentModel.DataAnnotations;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MentalAccounting.Web.Pages;

public class ConditionInput
{
    [Required, RegularExpression("^[a-zA-Z0-9_-]{1,64}$")]
    public string Key { get; set; } = Guid.NewGuid().ToString("N");
    [Required(ErrorMessage = "Inserire la decorrenza.")]
    public DateOnly? From { get; set; }
    [Required(ErrorMessage = "Inserire la percentuale."), Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ErrorMessage = "La percentuale deve essere tra 0 e 100.")]
    public decimal? Percentage { get; set; }
}
public sealed class MovementInput
{
    [Required, RegularExpression("^[a-zA-Z0-9_-]{1,64}$")]
    public string Key { get; set; } = Guid.NewGuid().ToString("N");
    [Required(ErrorMessage = "Inserire la data.")]
    public DateOnly? Date { get; set; }
    [Required(ErrorMessage = "Scegliere il tipo."), EnumDataType(typeof(MovementKind))]
    public MovementKind? Kind { get; set; } = MovementKind.Deposit;
    [Required(ErrorMessage = "Inserire l'importo."), Range(typeof(decimal), "0.01", "1000000000000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Inserire un importo positivo valido.")]
    public decimal? Amount { get; set; }
    [StringLength(500, ErrorMessage = "Massimo 500 caratteri.")]
    public string? Note { get; set; }
}
public sealed record EditorGrid(string Prefix, string Title, List<ConditionInput>? Conditions = null, List<MovementInput>? Movements = null);
public sealed record ConditionRow(string Prefix, ConditionInput Row);
public sealed record MovementRow(string Prefix, MovementInput Row);
public sealed record ScenarioGrid(IEnumerable<MentalAccounting.Application.Scenario> Items, bool Actions = false, long Version = 0);

public static class GridInputValidation
{
    public static void Conditions(List<ConditionInput> rows, string prefix, ModelStateDictionary state, DateOnly? after = null)
    {
        if (rows.Count > 1000) state.AddModelError(prefix, "Massimo 1000 righe.");
        if (rows.Select(x => x.Key).Distinct().Count() != rows.Count) state.AddModelError(prefix, "Identificativi delle righe duplicati.");
        foreach (var group in rows.Where(x => x.From.HasValue).GroupBy(x => x.From).Where(x => x.Count() > 1))
            foreach (var row in group) state.AddModelError($"{prefix}[{row.Key}].From", "Decorrenza duplicata.");
        if (after.HasValue)
            foreach (var row in rows.Where(x => x.From <= after)) state.AddModelError($"{prefix}[{row.Key}].From", "Usare una data successiva a oggi.");
    }
}
