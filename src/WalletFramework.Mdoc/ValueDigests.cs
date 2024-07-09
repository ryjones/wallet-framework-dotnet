using PeterO.Cbor;
using WalletFramework.Functional;
using static WalletFramework.Mdoc.Digest;
using static WalletFramework.Mdoc.DigestId;
using static WalletFramework.Mdoc.NameSpace;

namespace WalletFramework.Mdoc;

public readonly struct ValueDigests
{
    public Dictionary<NameSpace, Dictionary<DigestId, Digest>> Value { get; }

    private ValueDigests(Dictionary<NameSpace, Dictionary<DigestId, Digest>> value) => Value = value;
    
    public Dictionary<DigestId, Digest> this[NameSpace key] => Value[key];
    
    public static Validation<ValueDigests> ValidValueDigests(CBORObject valueDigests)
    {
        var validDict = valueDigests.ToDictionary(
            ValidNameSpace,
            digestsMap => digestsMap.ToDictionary(ValidDigestId, ValidDigest));

        return
            from dict in validDict
            select new ValueDigests(dict);
    }
}
