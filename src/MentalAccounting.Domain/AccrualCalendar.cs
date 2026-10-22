namespace MentalAccounting.Domain;

public static class AccrualCalendar
{
    public static DateOnly PeriodEnd(DateOnly day, CalendarRule rule)
    {
        if (rule.Months is not (1 or 3 or 6 or 12) || rule.CreditDay is < 1 or > 31)
            throw new BusinessException("Calendario non valido.");
        if (rule.Civil)
        {
            var month = ((day.Month - 1) / rule.Months) * rule.Months + 1;
            return new DateOnly(day.Year, month, 1).AddMonths(rule.Months).AddDays(-1);
        }
        var anchor = rule.Anchor ?? throw new BusinessException("Indicare la data di riferimento del calendario.");
        if (anchor.Year is < 1900 or > 2200) throw new BusinessException("Data di riferimento fuori intervallo.");
        var months = (day.Year - anchor.Year) * 12 + day.Month - anchor.Month;
        var index = (int)Math.Floor((decimal)months / rule.Months);
        while (anchor.AddMonths(index * rule.Months) > day) index--;
        while (anchor.AddMonths((index + 1) * rule.Months) <= day) index++;
        return anchor.AddMonths((index + 1) * rule.Months).AddDays(-1);
    }

    public static DateOnly CreditDate(DateOnly end, CalendarRule rule)
    {
        if (rule.Timing == CreditTiming.PeriodEnd) return end;
        var month = new DateOnly(end.Year, end.Month, 1).AddMonths(1);
        var day = rule.Timing == CreditTiming.NextMonthFirst ? 1 : rule.CreditDay;
        return new DateOnly(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
    }
}
