using Hyperledger.Aries;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Storage;
using Hyperledger.Indy.WalletApi;
using SD_JWT.Models;
using SD_JWT.Roles;
using WalletFramework.SdJwtVc.Models.Records;

namespace WalletFramework.SdJwtVc.Services.SdJwtVcHolderService;

/// <inheritdoc />
public class SdJwtVcHolderService : ISdJwtVcHolderService
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SdJwtVcHolderService" /> class.
    /// </summary>
    /// <param name="sdJwtSignerService">The service responsible for SD-JWT signature related operations.</param>
    /// <param name="recordService">The service responsible for wallet record operations.</param>
    /// <param name="holder">The service responsible for holder operations.</param>
    public SdJwtVcHolderService(
        IHolder holder,
        ISdJwtSignerService sdJwtSignerService,
        IWalletRecordService recordService)
    {
        _holder = holder;
        _sdJwtSignerService = sdJwtSignerService;
        _recordService = recordService;
    }

    private readonly IHolder _holder;
    private readonly ISdJwtSignerService _sdJwtSignerService;
    private readonly IWalletRecordService _recordService;

    /// <inheritdoc />
    public async Task<string> CreatePresentation(
        SdJwtRecord credential,
        string[] disclosedClaimPaths,
        string? audience = null,
        string? nonce = null)
    {
        var sdJwtDoc = credential.ToSdJwtDoc();
        var disclosures = new List<Disclosure>();
        foreach (var disclosure in sdJwtDoc.Disclosures)
        {
            if (disclosedClaimPaths.Any(disclosedClaim => disclosedClaim.StartsWith(disclosure.Path ?? string.Empty)))
            {
                disclosures.Add(disclosure);
            }
        }
    
        var presentationFormat =
            _holder.CreatePresentationFormat(credential.EncodedIssuerSignedJwt, disclosures.ToArray());
    
        if (!string.IsNullOrEmpty(credential.KeyId) 
            && !string.IsNullOrEmpty(nonce) 
            && !string.IsNullOrEmpty(audience))
        {
            var keybindingJwt = await _sdJwtSignerService.GenerateKbProofOfPossessionAsync(
                credential.KeyId,
                audience,
                nonce,
                "kb+jwt",
                presentationFormat.ToSdHash(),
                null);
            
            return presentationFormat.AddKeyBindingJwt(keybindingJwt);
        }
    
        return presentationFormat.Value;
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteAsync(IAgentContext context, string recordId) =>
        await _recordService.DeleteAsync<SdJwtRecord>(context.Wallet, recordId);

    /// <inheritdoc />
    public async Task<SdJwtRecord> GetAsync(IAgentContext context, string credentialId)
    {
        var record = await _recordService.GetAsync<SdJwtRecord>(context.Wallet, credentialId);
        if (record == null)
            throw new AriesFrameworkException(ErrorCode.RecordNotFound, "SD-JWT Credential record not found");

        return record;
    }

    /// <inheritdoc />
    public Task<List<SdJwtRecord>> ListAsync(
        IAgentContext context,
        ISearchQuery? query = null,
        int count = 100,
        int skip = 0) => _recordService.SearchAsync<SdJwtRecord>(context.Wallet, query, null, count, skip);

    /// <inheritdoc />
    public virtual async Task SaveAsync(IAgentContext context, SdJwtRecord record)
    {
        try
        {
            await _recordService.AddAsync(context.Wallet, record);
        }
        catch (WalletItemAlreadyExistsException)
        {
            await _recordService.UpdateAsync(context.Wallet, record);
        }
    }
}

internal static class SdJwtRecordExtensions
{
    internal static SdJwtDoc ToSdJwtDoc(this SdJwtRecord record) =>
        new(record.EncodedIssuerSignedJwt + "~" + string.Join("~", record.Disclosures) + "~");
}
