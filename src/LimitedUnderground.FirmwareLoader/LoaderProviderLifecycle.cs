namespace LimitedUnderground.FirmwareLoader;

public static class LoaderProviderLifecycle
{
    public const uint CurrentContractVersion = 1;
}

public sealed record LoaderTargetRule(
    string TargetKey,
    string ManifestSha256);

public sealed class LoaderTargetRuleSet
{
    public LoaderTargetRuleSet(
        string productKey,
        string providerKey,
        uint providerContractVersion,
        string sourceRevision,
        IEnumerable<LoaderTargetRule> targets)
    {
        ProductKey = LoaderProviderContractValidation.ProductKey(productKey);
        ProviderKey = LoaderProviderContractValidation.Identifier(
            providerKey,
            nameof(providerKey));
        ProviderContractVersion = LoaderProviderContractValidation.ContractVersion(
            providerContractVersion,
            nameof(providerContractVersion));
        SourceRevision = LoaderProviderContractValidation.LowerHex(
            sourceRevision,
            64,
            nameof(sourceRevision));
        ArgumentNullException.ThrowIfNull(targets);

        var copy = targets.ToArray();
        if (copy.Length is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targets),
                "A target rule set must contain between one and 32 targets.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in copy)
        {
            ArgumentNullException.ThrowIfNull(target);
            LoaderProviderContractValidation.TargetIdentifier(
                target.TargetKey,
                nameof(targets));
            LoaderProviderContractValidation.LowerHex(
                target.ManifestSha256,
                64,
                nameof(targets));
            if (!keys.Add(target.TargetKey))
            {
                throw new ArgumentException(
                    "Target keys must be unique.",
                    nameof(targets));
            }
        }

        Targets = Array.AsReadOnly(copy);
    }

    public string ProductKey { get; }

    public string ProviderKey { get; }

    public uint ProviderContractVersion { get; }

    public string SourceRevision { get; }

    public IReadOnlyList<LoaderTargetRule> Targets { get; }
}

public sealed record LoaderTrustedSigner(
    string SignerId,
    string PublicKeySha256);

public sealed class LoaderSignerTrustPolicy
{
    public LoaderSignerTrustPolicy(
        string productKey,
        string providerKey,
        uint providerContractVersion,
        string sourceRevision,
        IEnumerable<LoaderTrustedSigner> trustedSigners)
    {
        ProductKey = LoaderProviderContractValidation.ProductKey(productKey);
        ProviderKey = LoaderProviderContractValidation.Identifier(
            providerKey,
            nameof(providerKey));
        ProviderContractVersion = LoaderProviderContractValidation.ContractVersion(
            providerContractVersion,
            nameof(providerContractVersion));
        SourceRevision = LoaderProviderContractValidation.LowerHex(
            sourceRevision,
            64,
            nameof(sourceRevision));
        ArgumentNullException.ThrowIfNull(trustedSigners);

        var copy = trustedSigners.ToArray();
        if (copy.Length is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trustedSigners),
                "A signer policy must contain between one and 16 signers.");
        }

        var signerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var signer in copy)
        {
            ArgumentNullException.ThrowIfNull(signer);
            LoaderProviderContractValidation.LowerHex(
                signer.SignerId,
                16,
                nameof(trustedSigners));
            LoaderProviderContractValidation.LowerHex(
                signer.PublicKeySha256,
                64,
                nameof(trustedSigners));
            if (!signerIds.Add(signer.SignerId))
            {
                throw new ArgumentException(
                    "Signer identifiers must be unique.",
                    nameof(trustedSigners));
            }
        }

        TrustedSigners = Array.AsReadOnly(copy);
    }

    public string ProductKey { get; }

    public string ProviderKey { get; }

    public uint ProviderContractVersion { get; }

    public string SourceRevision { get; }

    public IReadOnlyList<LoaderTrustedSigner> TrustedSigners { get; }
}

public sealed class LoaderProviderOpenContext
{
    internal LoaderProviderOpenContext(
        object ownerToken,
        object activationToken,
        ulong sessionRevision,
        ulong providerGeneration,
        string productKey,
        string providerKey,
        uint providerContractVersion)
    {
        if (sessionRevision == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionRevision));
        }
        if (providerGeneration == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(providerGeneration));
        }

        OwnerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
        ActivationToken = activationToken ?? throw new ArgumentNullException(nameof(activationToken));
        SessionRevision = sessionRevision;
        ProviderGeneration = providerGeneration;
        ProductKey = LoaderProviderContractValidation.ProductKey(productKey);
        ProviderKey = LoaderProviderContractValidation.Identifier(
            providerKey,
            nameof(providerKey));
        ProviderContractVersion = LoaderProviderContractValidation.ContractVersion(
            providerContractVersion,
            nameof(providerContractVersion));
    }

    public ulong SessionRevision { get; }

    public ulong ProviderGeneration { get; }

    public string ProductKey { get; }

    public string ProviderKey { get; }

    public uint ProviderContractVersion { get; }

    internal object OwnerToken { get; }

    internal object ActivationToken { get; }
}

public interface ILoaderProductProviderFactory
{
    string ProductKey { get; }

    string ProviderKey { get; }

    uint ProviderContractVersion { get; }

    ILoaderProductProvider Open(LoaderProviderOpenContext context);
}

public interface ILoaderProductProvider
{
    LoaderProviderOpenContext Context { get; }

    LoaderTargetRuleSet TargetRules { get; }

    void Close();
}

internal static class LoaderProviderContractValidation
{
    internal static string ProductKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (LoaderProductCatalog.Find(value) is null)
        {
            throw new ArgumentException(
                "The product key is not in the shared loader catalog.",
                nameof(value));
        }

        return value;
    }

    internal static uint ContractVersion(uint value, string parameterName)
    {
        if (value != LoaderProviderLifecycle.CurrentContractVersion)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The provider lifecycle contract version is not supported.");
        }

        return value;
    }

    internal static string TargetIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 64 ||
            value[0] is < 'a' or > 'z' ||
            value.Any(character =>
                character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '_'))
        {
            throw new ArgumentException(
                "Target keys must be one to 64 lowercase ASCII letters, digits, or underscores and begin with a letter.",
                parameterName);
        }

        return value;
    }

    internal static string Identifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 64 ||
            value[0] is < 'a' or > 'z' ||
            value.Any(character =>
                character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '_' and
                not '-'))
        {
            throw new ArgumentException(
                "Identifiers must be one to 64 lowercase ASCII letters, digits, underscores, or hyphens and begin with a letter.",
                parameterName);
        }

        return value;
    }

    internal static string LowerHex(
        string value,
        int exactLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != exactLength ||
            value.All(character => character == '0') ||
            value.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                $"Value must be exactly {exactLength} nonzero lowercase hexadecimal characters.",
                parameterName);
        }

        return value;
    }
}
