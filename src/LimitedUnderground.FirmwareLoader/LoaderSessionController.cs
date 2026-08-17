using System.Threading;

namespace LimitedUnderground.FirmwareLoader;

public sealed class LoaderBundleInspectionContext
{
    internal LoaderBundleInspectionContext(
        object ownerToken,
        object activationToken,
        LoaderProviderOpenContext providerContext,
        ulong sessionRevision,
        ulong providerGeneration,
        string productKey,
        string providerKey,
        uint providerContractVersion,
        string targetRulesSourceRevision,
        string? signerTrustSourceRevision)
    {
        OwnerToken = ownerToken;
        ActivationToken = activationToken;
        ProviderContext = providerContext;
        SessionRevision = sessionRevision;
        ProviderGeneration = providerGeneration;
        ProductKey = productKey;
        ProviderKey = providerKey;
        ProviderContractVersion = providerContractVersion;
        TargetRulesSourceRevision = targetRulesSourceRevision;
        SignerTrustSourceRevision = signerTrustSourceRevision;
    }

    public ulong SessionRevision { get; }

    public ulong ProviderGeneration { get; }

    public string ProductKey { get; }

    public string ProviderKey { get; }

    public uint ProviderContractVersion { get; }

    public string TargetRulesSourceRevision { get; }

    public string? SignerTrustSourceRevision { get; }

    internal object OwnerToken { get; }

    internal object ActivationToken { get; }

    internal LoaderProviderOpenContext ProviderContext { get; }
}

public sealed record LoaderSessionSnapshot(
    ulong Revision,
    LoaderProductFamily? Product,
    bool ProviderActive,
    string? ProviderKey,
    uint? ProviderContractVersion,
    ulong? ProviderGeneration,
    string? TargetRulesSourceRevision,
    string? SignerTrustSourceRevision,
    string Status)
{
    public bool HasProduct => Product is not null;

    public bool ConnectedDeviceInspectionAvailable =>
        Product?.ConnectedDeviceInspectionAvailable ?? false;

    public bool FirmwareBundleSelectionAvailable =>
        Product?.FirmwareBundleSelectionAvailable ?? false;

    public bool DeviceBundleMatchAvailable =>
        Product?.DeviceBundleMatchAvailable ?? false;

    public bool FirmwareWritingAvailable =>
        Product?.FirmwareWritingAvailable ?? false;

    public bool RecoveryAvailable => Product?.RecoveryAvailable ?? false;
}

public sealed class LoaderSessionController : IDisposable
{
    private readonly object stateLock = new();
    private readonly object inspectionOwnerToken = new();
    private readonly IReadOnlyDictionary<string, ProviderFactoryRegistration>
        providerFactories;
    private readonly IReadOnlyDictionary<ProviderIdentity, LoaderSignerTrustPolicy>
        signerTrustPolicies;
    private ulong revision;
    private ulong providerGeneration;
    private LoaderProductFamily? selectedProduct;
    private ProviderActivationLease? activeLease;
    private LoaderProviderOpenContext? activeProviderContext;
    private LoaderTargetRuleSet? activeTargetRules;
    private LoaderSignerTrustPolicy? activeSignerTrust;
    private string? providerStatus;
    private bool transitioning;
    private bool disposed;

    public LoaderSessionController()
        : this(
            Array.Empty<ILoaderProductProviderFactory>(),
            Array.Empty<LoaderSignerTrustPolicy>())
    {
    }

    public LoaderSessionController(
        IEnumerable<ILoaderProductProviderFactory> providerFactories,
        IEnumerable<LoaderSignerTrustPolicy>? signerTrustPolicies = null)
    {
        ArgumentNullException.ThrowIfNull(providerFactories);
        var factoryCopy = new Dictionary<string, ProviderFactoryRegistration>(
            StringComparer.Ordinal);
        foreach (var factory in providerFactories)
        {
            ArgumentNullException.ThrowIfNull(factory);
            var productKey = LoaderProviderContractValidation.ProductKey(
                factory.ProductKey);
            var providerKey = LoaderProviderContractValidation.Identifier(
                factory.ProviderKey,
                nameof(providerFactories));
            var providerContractVersion =
                LoaderProviderContractValidation.ContractVersion(
                    factory.ProviderContractVersion,
                    nameof(providerFactories));
            var registration = new ProviderFactoryRegistration(
                factory,
                productKey,
                providerKey,
                providerContractVersion);
            if (!factoryCopy.TryAdd(productKey, registration))
            {
                throw new ArgumentException(
                    "Only one provider factory may be registered for a product.",
                    nameof(providerFactories));
            }
        }
        this.providerFactories = factoryCopy;

        var trustCopy = new Dictionary<ProviderIdentity, LoaderSignerTrustPolicy>();
        foreach (var policy in signerTrustPolicies ?? Array.Empty<LoaderSignerTrustPolicy>())
        {
            ArgumentNullException.ThrowIfNull(policy);
            var identity = ProviderIdentity.From(policy);
            if (!factoryCopy.TryGetValue(
                    policy.ProductKey,
                    out var registration) ||
                !identity.Equals(registration.Identity))
            {
                throw new ArgumentException(
                    "Signer trust must bind to an exact registered provider identity.",
                    nameof(signerTrustPolicies));
            }
            if (!trustCopy.TryAdd(identity, policy))
            {
                throw new ArgumentException(
                    "Only one signer-trust policy may be registered for a provider identity.",
                    nameof(signerTrustPolicies));
            }
        }
        this.signerTrustPolicies = trustCopy;
    }

    public LoaderSessionSnapshot Snapshot
    {
        get
        {
            lock (stateLock)
            {
                return CreateSnapshotLocked();
            }
        }
    }

    public bool SelectProduct(string engineeringKey)
    {
        LoaderProductFamily? nextProduct;
        ProviderActivationLease? previousLease;
        ProviderFactoryRegistration? registration;
        LoaderProviderOpenContext openContext;

        lock (stateLock)
        {
            if (disposed || transitioning)
            {
                return false;
            }

            nextProduct = LoaderProductCatalog.Find(engineeringKey);
            if (nextProduct is null)
            {
                return false;
            }

            if (string.Equals(
                    selectedProduct?.EngineeringKey,
                    nextProduct.EngineeringKey,
                    StringComparison.Ordinal))
            {
                return true;
            }

            transitioning = true;
            previousLease = DetachActiveLeaseLocked();
            selectedProduct = nextProduct;
            revision = checked(revision + 1);
            providerStatus = null;
            providerFactories.TryGetValue(
                nextProduct.EngineeringKey,
                out registration);
        }

        if (previousLease is not null && !previousLease.CloseOnce())
        {
            lock (stateLock)
            {
                if (!disposed)
                {
                    providerStatus =
                        "The previous provider could not close. No replacement provider was opened.";
                    transitioning = false;
                }
                return !disposed;
            }
        }

        lock (stateLock)
        {
            if (disposed)
            {
                transitioning = false;
                return false;
            }
            if (registration is null)
            {
                providerStatus = nextProduct.ProviderStatus;
                transitioning = false;
                return true;
            }

            providerGeneration = checked(providerGeneration + 1);
            openContext = new LoaderProviderOpenContext(
                inspectionOwnerToken,
                new object(),
                revision,
                providerGeneration,
                nextProduct.EngineeringKey,
                registration.ProviderKey,
                registration.ProviderContractVersion);
        }

        ILoaderProductProvider? provider = null;
        ProviderActivationLease? candidateLease = null;
        LoaderTargetRuleSet? candidateRules = null;
        var candidateValid = false;
        try
        {
            provider = registration.Factory.Open(openContext);
            if (provider is not null)
            {
                candidateLease = new ProviderActivationLease(provider);
                candidateRules = provider.TargetRules;
                candidateValid = ProviderIsValid(provider, candidateRules, openContext);
            }
        }
        catch
        {
            // Provider exceptions are contained and never published.
        }

        if (!candidateValid)
        {
            _ = candidateLease?.CloseOnce();
            lock (stateLock)
            {
                if (!disposed)
                {
                    providerStatus =
                        "The configured provider could not open. Inspection remains unavailable.";
                    transitioning = false;
                }
                return !disposed;
            }
        }

        var shouldCloseCandidate = false;
        lock (stateLock)
        {
            if (disposed)
            {
                shouldCloseCandidate = true;
                transitioning = false;
            }
            else
            {
                var identity = ProviderIdentity.From(openContext);
                activeLease = candidateLease;
                activeProviderContext = openContext;
                activeTargetRules = candidateRules;
                signerTrustPolicies.TryGetValue(identity, out activeSignerTrust);
                providerStatus = activeSignerTrust is null
                    ? "Provider lifecycle v1 target rules are active. Signer trust and firmware installation remain disabled."
                    : "Provider lifecycle v1 target rules and an independent signer policy are active. Cryptographic admission and firmware installation remain disabled.";
                transitioning = false;
            }
        }

        if (shouldCloseCandidate)
        {
            _ = candidateLease?.CloseOnce();
        }

        lock (stateLock)
        {
            return !disposed;
        }
    }

    public void ReturnToProductChoice()
    {
        ProviderActivationLease? previousLease;
        lock (stateLock)
        {
            if (disposed || transitioning || selectedProduct is null)
            {
                return;
            }

            transitioning = true;
            previousLease = DetachActiveLeaseLocked();
            selectedProduct = null;
            providerStatus = null;
            revision = checked(revision + 1);
        }

        _ = previousLease?.CloseOnce();
        lock (stateLock)
        {
            if (!disposed)
            {
                transitioning = false;
            }
        }
    }

    public bool TryCreateOfflineBundleInspectionContext(
        out LoaderBundleInspectionContext? context)
    {
        lock (stateLock)
        {
            if (disposed ||
                transitioning ||
                selectedProduct is null ||
                activeLease is null ||
                activeProviderContext is null ||
                activeTargetRules is null ||
                revision == 0)
            {
                context = null;
                return false;
            }

            context = new LoaderBundleInspectionContext(
                inspectionOwnerToken,
                activeProviderContext.ActivationToken,
                activeProviderContext,
                revision,
                activeProviderContext.ProviderGeneration,
                selectedProduct.EngineeringKey,
                activeProviderContext.ProviderKey,
                activeProviderContext.ProviderContractVersion,
                activeTargetRules.SourceRevision,
                activeSignerTrust?.SourceRevision);
            return true;
        }
    }

    public bool CanPublishOfflineBundleInspection(
        LoaderBundleInspectionContext context,
        FirmwareBundleCandidateResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        lock (stateLock)
        {
            if (disposed ||
                transitioning ||
                selectedProduct is null ||
                activeLease is null ||
                activeProviderContext is null ||
                activeTargetRules is null)
            {
                return false;
            }

            return ReferenceEquals(context.OwnerToken, inspectionOwnerToken) &&
                ReferenceEquals(result.Context, context) &&
                ReferenceEquals(context.ProviderContext, activeProviderContext) &&
                ReferenceEquals(
                    context.ActivationToken,
                    activeProviderContext.ActivationToken) &&
                context.SessionRevision == revision &&
                result.SessionRevision == revision &&
                context.ProviderGeneration == activeProviderContext.ProviderGeneration &&
                context.ProviderGeneration != 0 &&
                context.ProviderContractVersion ==
                    activeProviderContext.ProviderContractVersion &&
                context.ProviderContractVersion ==
                    LoaderProviderLifecycle.CurrentContractVersion &&
                string.Equals(
                    context.ProductKey,
                    selectedProduct.EngineeringKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    context.ProductKey,
                    activeProviderContext.ProductKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    context.ProviderKey,
                    activeProviderContext.ProviderKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    context.TargetRulesSourceRevision,
                    activeTargetRules.SourceRevision,
                    StringComparison.Ordinal) &&
                string.Equals(
                    context.SignerTrustSourceRevision,
                    activeSignerTrust?.SourceRevision,
                    StringComparison.Ordinal) &&
                result.StructureVerified &&
                result.ImageDigestVerified &&
                result.SignaturePresent &&
                result.ProductMatched &&
                string.Equals(
                    result.ProductKey,
                    selectedProduct.EngineeringKey,
                    StringComparison.Ordinal) &&
                activeTargetRules.Targets.Any(target =>
                    string.Equals(
                        target.TargetKey,
                        result.TargetKey,
                        StringComparison.Ordinal)) &&
                !result.SignerTrusted &&
                !result.AdmissionAllowed;
        }
    }

    public void Dispose()
    {
        ProviderActivationLease? previousLease;
        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            transitioning = true;
            var hadSelection = selectedProduct is not null;
            previousLease = DetachActiveLeaseLocked();
            selectedProduct = null;
            providerStatus = null;
            if (hadSelection)
            {
                revision = checked(revision + 1);
            }
        }

        _ = previousLease?.CloseOnce();
    }

    private ProviderActivationLease? DetachActiveLeaseLocked()
    {
        var lease = activeLease;
        activeLease = null;
        activeProviderContext = null;
        activeTargetRules = null;
        activeSignerTrust = null;
        return lease;
    }

    private static bool ProviderIsValid(
        ILoaderProductProvider provider,
        LoaderTargetRuleSet rules,
        LoaderProviderOpenContext context) =>
        ReferenceEquals(provider.Context, context) &&
        string.Equals(rules.ProductKey, context.ProductKey, StringComparison.Ordinal) &&
        string.Equals(rules.ProviderKey, context.ProviderKey, StringComparison.Ordinal) &&
        rules.ProviderContractVersion == context.ProviderContractVersion;

    private LoaderSessionSnapshot CreateSnapshotLocked()
    {
        if (disposed)
        {
            return new LoaderSessionSnapshot(
                revision,
                Product: null,
                ProviderActive: false,
                ProviderKey: null,
                ProviderContractVersion: null,
                ProviderGeneration: null,
                TargetRulesSourceRevision: null,
                SignerTrustSourceRevision: null,
                Status: "The loader session is closed.");
        }

        if (selectedProduct is null)
        {
            return new LoaderSessionSnapshot(
                revision,
                Product: null,
                ProviderActive: false,
                ProviderKey: null,
                ProviderContractVersion: null,
                ProviderGeneration: null,
                TargetRulesSourceRevision: null,
                SignerTrustSourceRevision: null,
                Status: "Choose the Limited Underground system you are working with.");
        }

        return new LoaderSessionSnapshot(
            revision,
            selectedProduct,
            ProviderActive: activeLease is not null,
            ProviderKey: activeProviderContext?.ProviderKey,
            ProviderContractVersion: activeProviderContext?.ProviderContractVersion,
            ProviderGeneration: activeProviderContext?.ProviderGeneration,
            TargetRulesSourceRevision: activeTargetRules?.SourceRevision,
            SignerTrustSourceRevision: activeSignerTrust?.SourceRevision,
            Status: transitioning
                ? "The selected provider is changing. Operations remain unavailable."
                : providerStatus ??
                    $"{selectedProduct.DisplayName} selected. Inspection provider unavailable; firmware installation remains disabled.");
    }

    private readonly record struct ProviderIdentity(
        string ProductKey,
        string ProviderKey,
        uint ProviderContractVersion)
    {

        internal static ProviderIdentity From(LoaderProviderOpenContext context) =>
            new(context.ProductKey, context.ProviderKey, context.ProviderContractVersion);

        internal static ProviderIdentity From(LoaderSignerTrustPolicy policy) =>
            new(policy.ProductKey, policy.ProviderKey, policy.ProviderContractVersion);
    }

    private sealed record ProviderFactoryRegistration(
        ILoaderProductProviderFactory Factory,
        string ProductKey,
        string ProviderKey,
        uint ProviderContractVersion)
    {
        internal ProviderIdentity Identity =>
            new(ProductKey, ProviderKey, ProviderContractVersion);
    }

    private sealed class ProviderActivationLease
    {
        private int closeStarted;
        private int closeSucceeded;

        internal ProviderActivationLease(ILoaderProductProvider provider)
        {
            Provider = provider;
        }

        internal ILoaderProductProvider Provider { get; }

        internal bool CloseOnce()
        {
            if (Interlocked.Exchange(ref closeStarted, 1) != 0)
            {
                return Volatile.Read(ref closeSucceeded) == 1;
            }

            try
            {
                Provider.Close();
                Volatile.Write(ref closeSucceeded, 1);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
