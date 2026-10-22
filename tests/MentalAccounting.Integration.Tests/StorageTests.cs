using ClosedXML.Excel;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using MentalAccounting.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace MentalAccounting.Integration.Tests;
public sealed class PostgresFactAttribute:FactAttribute
{
    public PostgresFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_POSTGRES")))Skip="Impostare TEST_POSTGRES con un database dedicato *_test.";}
}
public sealed class WorkbookFactAttribute:FactAttribute
{
    public WorkbookFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_WORKBOOK")))Skip="Impostare TEST_WORKBOOK per il foglio personale locale.";}
}
[Collection("Postgres")]
public sealed class StorageTests
{
    [PostgresFact] public async Task MigrationsRoundTripBackupAndOptimisticConcurrency()
    {
        var connection=Environment.GetEnvironmentVariable("TEST_POSTGRES")!;
        var cs=new NpgsqlConnectionStringBuilder(connection);
        Assert.EndsWith("_test",cs.Database);
        var options=new DbContextOptionsBuilder<AccountingDbContext>().UseNpgsql(connection).Options;
        await using var db=new AccountingDbContext(options);
        await db.Database.MigrateAsync();await db.Accounts.ExecuteDeleteAsync();
        var start=new DateOnly(2026,1,1);
        var doc=new AccountDocument(1,new(start,1000.12m,0,0,DayBasis.Actual365,new(),[],[new(start,.025m)],[new(start,.26m)]),[]);
        var store=new PostgresAccountStore(db,TimeProvider.System);await store.Save(doc,0);
        var loaded=await store.Load();Assert.Equal(1000.12m,loaded.Document.Ledger!.OpeningBalance);
        var backup=BackupFormat.Write(loaded.Document);
        await using var db2=new AccountingDbContext(options);var second=new PostgresAccountStore(db2,TimeProvider.System);
        await second.Save(loaded.Document,1);
        await Assert.ThrowsAsync<ConcurrencyException>(()=>store.Save(doc,1));
        await db.Accounts.ExecuteDeleteAsync();await store.Save(BackupFormat.Read(backup),0);
        Assert.Equal(backup,BackupFormat.Write((await store.Load()).Document));
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact] public void WorkbookMappingSkipsDerivedRowsAndRejectsFormulas()
    {
        using var book=new XLWorkbook();var s=book.AddWorksheet("Sheet1");
        s.Cell("C2").Value=new DateTime(2026,1,1);s.Cell("D2").Value="Carico";s.Cell("E2").Value=100.25;
        s.Cell("D3").Value="Consolidamento";s.Cell("E3").FormulaA1="SUM(E2)";
        s.Cell("C4").Value=new DateTime(2026,1,2);s.Cell("D4").Value="Carico";s.Cell("E4").FormulaA1="1+2";
        using var stream=new MemoryStream();book.SaveAs(stream);stream.Position=0;
        var result=new VacationWorkbookReader().Read(stream);Assert.Single(result.Movements);Assert.Equal(100.25m,result.Movements[0].Amount);
        Assert.Single(result.Warnings);Assert.Single(result.Errors);
    }
    [WorkbookFact] public void ActualWorkbookIsReadWithoutUsingCalculatedInterest()
    {
        using var stream=File.OpenRead(Environment.GetEnvironmentVariable("TEST_WORKBOOK")!);
        var result=new VacationWorkbookReader().Read(stream);
        Assert.NotEmpty(result.Movements);Assert.NotEmpty(result.Warnings);Assert.Empty(result.Errors);
        Assert.All(result.Movements,m=>Assert.True(m.Amount>0));
    }
}

