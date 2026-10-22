namespace MentalAccounting.Web;
public static class BusinessDate
{
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).DateTime);
}
