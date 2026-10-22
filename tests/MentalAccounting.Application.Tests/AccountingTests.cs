using MentalAccounting.Application;
using MentalAccounting.Domain;
namespace MentalAccounting.Application.Tests;
public sealed class AccountingTests
{
    private static readonly DateOnly Today=new(2026,10,22);
    private static AccountDocument Document()=>new(1,new(new(2026,1,1),1000,0,0,DayBasis.Actual365,new(),[],[new(new(2026,1,1),.02m)],[new(new(2026,1,1),.26m)]),[]);
    [Fact] public async Task PreviewDoesNotWriteAndCommitRejectsStaleVersion()
    {
        var store=new MemoryStore();var service=new AccountingService(store,new());
        var preview=await service.Preview(Document(),0,Today);Assert.Equal(0,(await store.Load()).Version);
        await service.Commit(preview.Document,0,Today);
        await Assert.ThrowsAsync<ConcurrencyException>(()=>service.Commit(Document(),0,Today));
    }
    [Fact] public async Task SimulationDoesNotAlterHistoryAndSurvivesBackup()
    {
        var store=new MemoryStore();var service=new AccountingService(store,new());var d=Document();
        var future=d.Ledger! with {Movements=[new(Guid.NewGuid(),Today.AddDays(1),MovementKind.Deposit,100)]};
        var scenario=new Scenario(Guid.NewGuid(),"Vacanze",Today.AddYears(1),InterestMethod.Simple,future);
        var proposed=d with {Scenarios=[scenario]};await service.Commit(proposed,0,Today);
        Assert.Empty((await service.Load()).Document.Ledger!.Movements);
        var copy=BackupFormat.Read(BackupFormat.Write(proposed));Assert.Equal(100,copy.Scenarios.Single().Ledger.Movements.Single().Amount);
    }
    [Fact] public async Task InvalidHistoricalWithdrawalIsRejectedBeforeSave()
    {
        var store=new MemoryStore();var service=new AccountingService(store,new());var d=Document();
        await Assert.ThrowsAsync<BusinessException>(()=>service.Commit(d with {Ledger=d.Ledger! with {Movements=[new(Guid.NewGuid(),d.Ledger.Start,MovementKind.Withdrawal,1001)]}},0,Today));
        Assert.Equal(0,(await store.Load()).Version);
    }
    [Fact] public async Task FutureMovementsBelongOnlyToScenarios()
    {
        var service=new AccountingService(new MemoryStore(),new());var d=Document();
        await Assert.ThrowsAsync<BusinessException>(()=>service.Commit(d with {Ledger=d.Ledger! with {Movements=[new(Guid.NewGuid(),Today.AddDays(1),MovementKind.Deposit,100)]}},0,Today));
    }
    [Fact] public void ParsesItalianPercentagesAndEscapesCsvFormulas()
    {
        Assert.Equal(.025m,EntryParser.Rates("2026-01-01;2,5").Single().Rate);
        var l=Document().Ledger! with {Movements=[new(Guid.NewGuid(),Today,MovementKind.Deposit,1,"=HYPERLINK(1)")]};
        Assert.Contains("'",AccountingService.MovementsCsv(l));
        Assert.Throws<BusinessException>(()=>BackupFormat.Read("{\"SchemaVersion\":9,\"Scenarios\":[]}"));
    }
    [Fact] public void MalformedBackupCannotCausePartialSave()
    {
        var service=new AccountingService(new MemoryStore(),new());
        Assert.Throws<BusinessException>(()=>service.ValidateDocument(Document() with {Ledger=Document().Ledger! with {Rates=null!}},Today));
    }
}
public sealed class MemoryStore : IAccountStore
{
    private AccountSnapshot snapshot=new(0,new(1,null,[]));
    public Task<AccountSnapshot> Load(CancellationToken ct=default)=>Task.FromResult(snapshot);
    public Task Save(AccountDocument document,long expectedVersion,CancellationToken ct=default)
    {
        if(snapshot.Version!=expectedVersion)throw new ConcurrencyException();
        snapshot=new(expectedVersion+1,BackupFormat.Read(BackupFormat.Write(document)));return Task.CompletedTask;
    }
}
