using Hyperledger.Aries;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Storage;
using LanguageExt;
using WalletFramework.Core.Credentials;
using WalletFramework.Core.Functional;
using WalletFramework.MdocVc;
using WalletFramework.Oid4Vc.CredentialSet.Models;
using WalletFramework.Oid4Vc.Oid4Vci.Abstractions;
using WalletFramework.SdJwtVc.Models.Records;
using WalletFramework.SdJwtVc.Services;
using WalletFramework.SdJwtVc.Services.SdJwtVcHolderService;

namespace WalletFramework.Oid4Vc.CredentialSet;

public class CredentialSetService(
    IAgentProvider agentProvider,
    ISdJwtVcHolderService sdJwtVcHolderService,
    IMdocStorage mDocStorage,
    IStatusListService statusListService,
    IWalletRecordService walletRecordService)
    : ICredentialSetService
{
    public async Task<Option<IEnumerable<SdJwtRecord>>> GetAssociatedSdJwtRecords(CredentialSetId credentialSetId)
    {
        var context = await agentProvider.GetContextAsync();
        
        var sdJwtQuery = SearchQuery.Equal(
            "~" + nameof(SdJwtRecord.CredentialSetId),
            credentialSetId);

        var sdJwtRecords = await sdJwtVcHolderService.ListAsync(
            context,
            sdJwtQuery);

        return sdJwtRecords.Any() 
            ? sdJwtRecords 
            : Option<IEnumerable<SdJwtRecord>>.None;
    }
    
    public async Task<Option<IEnumerable<MdocRecord>>> GetAssociatedMDocRecords(CredentialSetId credentialSetId)
    {
        var mDocQuery = SearchQuery.Equal(
            "~" + nameof(MdocRecord.CredentialSetId),
            credentialSetId);

        return await mDocStorage.List(
            Option<ISearchQuery>.Some(mDocQuery));
    }

    public virtual async Task DeleteAsync(CredentialSetId credentialSetId)
    {
        var context = await agentProvider.GetContextAsync();
        var credentialSetRecord = await walletRecordService.GetAsync<CredentialSetRecord>(context.Wallet, credentialSetId);
        if (credentialSetRecord == null)
            throw new AriesFrameworkException(ErrorCode.RecordNotFound, "CredentialSet record not found");
        
        var sdJwtRecords = await GetAssociatedSdJwtRecords(credentialSetId);
        await sdJwtRecords.Match(
            Some: async records =>
            {
                foreach (var record in records)
                    await sdJwtVcHolderService.DeleteAsync(context, record.Id);
            },
            None: () => Task.CompletedTask);
        
        var mDocRecords = await GetAssociatedMDocRecords(credentialSetId);
        await mDocRecords.Match(
            Some: async records =>
            {
                foreach (var record in records)
                    await mDocStorage.Delete(record);
            },
            None: () => Task.CompletedTask);

        credentialSetRecord.State = CredentialState.Deleted;
        credentialSetRecord.DeletedAt = DateTime.UtcNow;
        await walletRecordService.UpdateAsync(context.Wallet, credentialSetRecord);
    }

    public async Task AddAsync(CredentialSetRecord credentialSetRecord)
    {
        var context = await agentProvider.GetContextAsync();
        await walletRecordService.AddAsync(context.Wallet, credentialSetRecord);
    }
    
    public async Task<Option<IEnumerable<CredentialSetRecord>>> ListAsync(
        Option<ISearchQuery> query,
        int count = 100,
        int skip = 0)
    {
        var context = await agentProvider.GetContextAsync();
        var records = await walletRecordService.SearchAsync<CredentialSetRecord>(
            context.Wallet, 
            query.ToNullable(),
            null,
            count, 
            skip);
        
        if (records.Count == 0)
            return Option<IEnumerable<CredentialSetRecord>>.None;
        
        return records;
    }
    
    public async Task<Option<CredentialSetRecord>> GetAsync(CredentialSetId credentialSetId)
    {
        var context = await agentProvider.GetContextAsync();
        var record = await walletRecordService.GetAsync<CredentialSetRecord>(context.Wallet, credentialSetId);
        
        return record;
    }
    
    public virtual async Task UpdateAsync(CredentialSetRecord credentialSetRecord)
    {
        var context = await agentProvider.GetContextAsync();
        await walletRecordService.UpdateAsync(context.Wallet, credentialSetRecord);
    }
    
    public async Task<CredentialSetRecord> RefreshCredentialSetState(CredentialSetRecord credentialSetRecord)
    {
        var oldState = credentialSetRecord.State;
        
        if (credentialSetRecord.IsDeleted())
            return credentialSetRecord;
        
        credentialSetRecord.ExpiresAt.IfSome(expiresAt =>
        {
            if (expiresAt < DateTime.UtcNow)
                credentialSetRecord.State = CredentialState.Expired;
        });

        await credentialSetRecord.StatusList.IfSomeAsync(
            async statusList =>
            {
                await statusListService.GetState(statusList).IfSomeAsync(
                    state =>
                    {
                        if (state == CredentialState.Revoked)
                            credentialSetRecord.State = CredentialState.Revoked;
                    });
            });
        
        if (oldState != credentialSetRecord.State) 
            await UpdateAsync(credentialSetRecord);
        
        return credentialSetRecord;
    }

    public async Task RefreshCredentialSetStates()
    {
        var credentialSetRecords = await ListAsync(Option<ISearchQuery>.None);

        await credentialSetRecords.IfSomeAsync(
            async records =>
            {
                foreach (var credentialSetRecord in records)
                {
                    await RefreshCredentialSetState(credentialSetRecord);
                }
            });
    }
}
