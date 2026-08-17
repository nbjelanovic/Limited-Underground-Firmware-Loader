namespace LimitedUnderground.FirmwareLoader;

public static class LoaderProductionProviders
{
    public static IReadOnlyList<ILoaderProductProviderFactory> Factories { get; } =
        Array.AsReadOnly<ILoaderProductProviderFactory>(
        [new OpenTrailInspectionProviderFactory()]);

    public static IReadOnlyList<LoaderSignerTrustPolicy> SignerTrustPolicies { get; } =
        Array.Empty<LoaderSignerTrustPolicy>();
}

public sealed class OpenTrailInspectionProviderFactory : ILoaderProductProviderFactory
{
    public const string ExactProductKey = "opentrail";
    public const string ExactProviderKey = "opentrail";
    public const string HeltecV4BenchTargetKey = "heltec_v4_bench";

    // SHA-256 of the Git blob bytes for OpenTrail's public heltec_v4_bench target-contract.json at
    // OpenTrail commit a327104ac67a3f5918a8b0191c96dceb05b5399b.
    public const string TargetContractSha256 =
        "ec818efab9a14ce4f0900068c9474acfe2577d74e2e39fa4850f3ff0567e9776";

    public string ProductKey => ExactProductKey;

    public string ProviderKey => ExactProviderKey;

    public uint ProviderContractVersion => LoaderProviderLifecycle.CurrentContractVersion;

    public ILoaderProductProvider Open(LoaderProviderOpenContext context) =>
        new OpenTrailInspectionProvider(context);
}

internal sealed class OpenTrailInspectionProvider : ILoaderProductProvider
{
    internal OpenTrailInspectionProvider(LoaderProviderOpenContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        TargetRules = new LoaderTargetRuleSet(
            OpenTrailInspectionProviderFactory.ExactProductKey,
            OpenTrailInspectionProviderFactory.ExactProviderKey,
            LoaderProviderLifecycle.CurrentContractVersion,
            OpenTrailInspectionProviderFactory.TargetContractSha256,
            [
                new LoaderTargetRule(
                    OpenTrailInspectionProviderFactory.HeltecV4BenchTargetKey,
                    OpenTrailInspectionProviderFactory.TargetContractSha256),
            ]);
    }

    public LoaderProviderOpenContext Context { get; }

    public LoaderTargetRuleSet TargetRules { get; }

    public void Close()
    {
        // This provider owns immutable in-memory inspection rules only.
    }
}
