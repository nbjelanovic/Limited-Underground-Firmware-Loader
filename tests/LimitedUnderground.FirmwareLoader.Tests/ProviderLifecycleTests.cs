using LimitedUnderground.FirmwareLoader;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

internal static class ProviderLifecycleTests
{
    private const string RulesRevision =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string TrustRevision =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string ManifestRevision =
        "3333333333333333333333333333333333333333333333333333333333333333";
    private const string PublicKeyRevision =
        "4444444444444444444444444444444444444444444444444444444444444444";

    internal static IReadOnlyList<(string Name, Action Run)> All { get; } =
        new (string Name, Action Run)[]
        {
            ("production selection remains providerless", ProductionSelectionRemainsProviderless),
            ("provider registry rejects invalid identities", ProviderRegistryRejectsInvalidIdentities),
            ("provider registry rejects duplicates", ProviderRegistryRejectsDuplicates),
            ("provider registry freezes factory identity", ProviderRegistryFreezesFactoryIdentity),
            ("trust registry requires exact provider identity", TrustRegistryRequiresExactProviderIdentity),
            ("exact provider activation binds immutable identity", ExactProviderActivationBindsIdentity),
            ("same product reselection preserves lease", SameProductReselectionPreservesLease),
            ("switch closes old provider before replacement", SwitchClosesOldProviderBeforeReplacement),
            ("chooser return and disposal close once", ChooserReturnAndDisposalCloseOnce),
            ("throwing close blocks replacement", ThrowingCloseBlocksReplacement),
            ("failed factories remain providerless", FailedFactoriesRemainProviderless),
            ("mismatched provider is rejected and closed", MismatchedProviderIsRejectedAndClosed),
            ("reentrant close operations are suppressed", ReentrantCloseOperationsAreSuppressed),
            ("reentrant owner disposal aborts activation", ReentrantOwnerDisposalAbortsActivation),
            ("stale provider result cannot publish", StaleProviderResultCannotPublish),
            ("target rules require an exact allowed target", TargetRulesRequireExactAllowedTarget),
            ("signer trust stays independent from admission", SignerTrustStaysIndependentFromAdmission),
            ("rule and trust inputs are immutable copies", RuleAndTrustInputsAreImmutableCopies),
            ("target rule validation rejects unsafe identity", TargetRuleValidationRejectsUnsafeIdentity),
            ("provider surface contains no hardware authority", ProviderSurfaceContainsNoHardwareAuthority),
            ("provider failures expose only sanitized status", ProviderFailuresExposeOnlySanitizedStatus),
        };

    internal static LoaderSessionController CreateController(string productKey)
    {
        var providerKey = productKey + "_fixture";
        var factory = new FakeFactory(
            productKey,
            providerKey,
            context => new FakeProvider(
                context,
                CreateRules(productKey, providerKey)));
        return new LoaderSessionController(new[] { factory });
    }

    private static void ProductionSelectionRemainsProviderless()
    {
        var controller = new LoaderSessionController();
        Require(controller.SelectProduct("opentrail"), "production selection");
        Require(controller.Snapshot.HasProduct, "production selected product");
        Require(!controller.Snapshot.ProviderActive, "production provider inactive");
        Require(
            !controller.TryCreateOfflineBundleInspectionContext(out var context),
            "production context denied");
        Require(context is null, "production context null");
    }

    private static void ProviderRegistryRejectsInvalidIdentities()
    {
        ExpectArgument(
            () => new LoaderSessionController(new[]
            {
                new FakeFactory("unknown", "unknown_provider", _ => null!),
            }),
            "unknown product");
        ExpectArgument(
            () => new LoaderSessionController(new[]
            {
                new FakeFactory("opentrail", "Bad/Provider", _ => null!),
            }),
            "malformed provider key");
        ExpectArgument(
            () => new LoaderSessionController(new[]
            {
                new FakeFactory("opentrail", "Trail_provider", _ => null!),
            }),
            "uppercase provider key");
        ExpectArgument(
            () => new LoaderSessionController(new[]
            {
                new FakeFactory(
                    "opentrail",
                    "trail_provider",
                    _ => null!,
                    providerContractVersion: 0),
            }),
            "zero provider version");
        ExpectArgument(
            () => new LoaderSessionController(new[]
            {
                new FakeFactory(
                    "opentrail",
                    "trail_provider",
                    _ => null!,
                    providerContractVersion: 2),
            }),
            "unsupported provider version");
    }

    private static void ProviderRegistryRejectsDuplicates()
    {
        ExpectArgument(
            () => new LoaderSessionController(new ILoaderProductProviderFactory[]
            {
                new FakeFactory("opentrail", "trail_one", _ => null!),
                new FakeFactory("opentrail", "trail_two", _ => null!),
            }),
            "duplicate product provider");
    }
    private static void ProviderRegistryFreezesFactoryIdentity()
    {
        var factory = new MutableIdentityFactory();
        var controller = new LoaderSessionController(new[] { factory });
        factory.RejectIdentityReads();
        Require(controller.SelectProduct("opentrail"), "frozen identity selection");
        Require(controller.Snapshot.ProviderActive, "frozen identity active");
        Require(controller.Snapshot.ProviderKey == "trail_provider", "frozen provider key");
        Require(factory.ProductKeyReads == 1, "product key read once");
        Require(factory.ProviderKeyReads == 1, "provider key read once");
        Require(factory.ContractVersionReads == 1, "contract version read once");
        Require(factory.OpenCount == 1, "frozen identity open");
    }

    private static void TrustRegistryRequiresExactProviderIdentity()
    {
        var trailFactory = new FakeFactory(
            "opentrail",
            "trail_provider",
            context => new FakeProvider(
                context,
                CreateRules("opentrail", "trail_provider")));
        var orphan = CreateTrust("opengauge", "display_provider");
        ExpectArgument(
            () => new LoaderSessionController(new[] { trailFactory }, new[] { orphan }),
            "orphan trust");

        var mismatch = CreateTrust("opentrail", "other_provider");
        ExpectArgument(
            () => new LoaderSessionController(new[] { trailFactory }, new[] { mismatch }),
            "mismatched trust");

        ExpectArgument(
            () => new LoaderSignerTrustPolicy(
                "opentrail",
                "trail_provider",
                providerContractVersion: 2,
                TrustRevision,
                new[]
                {
                    new LoaderTrustedSigner(
                        "0123456789abcdef",
                        PublicKeyRevision),
                }),
            "unsupported trust version");

        var exact = CreateTrust("opentrail", "trail_provider");
        ExpectArgument(
            () => new LoaderSessionController(
                new[] { trailFactory },
                new[] { exact, exact }),
            "duplicate trust");
    }

    private static void ExactProviderActivationBindsIdentity()
    {
        var controller = CreateController("opentrail");
        Require(controller.SelectProduct("opentrail"), "provider selection");
        var snapshot = controller.Snapshot;
        Require(snapshot.ProviderActive, "provider active");
        Require(snapshot.ProviderKey == "opentrail_fixture", "provider key");
        Require(
            snapshot.ProviderContractVersion ==
                LoaderProviderLifecycle.CurrentContractVersion,
            "provider version");
        Require(snapshot.ProviderGeneration == 1, "provider generation");
        Require(snapshot.TargetRulesSourceRevision == RulesRevision, "rules revision");
        Require(snapshot.SignerTrustSourceRevision is null, "trust absent");
        Require(
            controller.TryCreateOfflineBundleInspectionContext(out var context),
            "provider context");
        Require(context is not null, "provider context value");
        Require(context!.ProviderGeneration == 1, "context provider generation");
        Require(context!.ProviderKey == "opentrail_fixture", "context provider key");
    }

    private static void SameProductReselectionPreservesLease()
    {
        var factory = CreateFactory("opentrail", "trail_provider");
        var controller = new LoaderSessionController(new[] { factory });
        Require(controller.SelectProduct("opentrail"), "initial selection");
        var before = controller.Snapshot;
        Require(controller.SelectProduct("opentrail"), "exact reselection");
        var after = controller.Snapshot;
        Require(factory.OpenCount == 1, "reselection open count");
        Require(factory.LastProvider?.CloseCount == 0, "reselection close count");
        Require(after.Revision == before.Revision, "reselection revision");
        Require(after.ProviderGeneration == before.ProviderGeneration, "reselection generation");
    }

    private static void SwitchClosesOldProviderBeforeReplacement()
    {
        var events = new List<string>();
        var trailFactory = new FakeFactory(
            "opentrail",
            "trail_provider",
            context =>
            {
                events.Add("trail-open");
                return new FakeProvider(
                    context,
                    CreateRules("opentrail", "trail_provider"),
                    () => events.Add("trail-close"));
            });
        var displayFactory = new FakeFactory(
            "opengauge",
            "display_provider",
            context =>
            {
                events.Add("display-open");
                return new FakeProvider(
                    context,
                    CreateRules("opengauge", "display_provider"));
            });
        var controller = new LoaderSessionController(
            new ILoaderProductProviderFactory[] { trailFactory, displayFactory });
        Require(controller.SelectProduct("opentrail"), "Trail open");
        Require(controller.SelectProduct("opengauge"), "Display open");
        Require(
            events.SequenceEqual(
                new[] { "trail-open", "trail-close", "display-open" },
                StringComparer.Ordinal),
            "switch order");
        Require(trailFactory.LastProvider?.CloseCount == 1, "old close once");
        Require(controller.Snapshot.ProviderGeneration == 2, "switch generation");
    }

    private static void ChooserReturnAndDisposalCloseOnce()
    {
        var firstFactory = CreateFactory("opentrail", "trail_provider");
        var first = new LoaderSessionController(new[] { firstFactory });
        Require(first.SelectProduct("opentrail"), "chooser setup");
        first.ReturnToProductChoice();
        first.ReturnToProductChoice();
        Require(firstFactory.LastProvider?.CloseCount == 1, "chooser close once");
        Require(!first.Snapshot.HasProduct, "chooser selected state");

        var secondFactory = CreateFactory("opengauge", "display_provider");
        var second = new LoaderSessionController(new[] { secondFactory });
        Require(second.SelectProduct("opengauge"), "dispose setup");
        second.Dispose();
        second.Dispose();
        Require(secondFactory.LastProvider?.CloseCount == 1, "dispose close once");
        Require(!second.SelectProduct("opentrail"), "post-dispose selection");
        Require(
            !second.TryCreateOfflineBundleInspectionContext(out _),
            "post-dispose context");
    }

    private static void ThrowingCloseBlocksReplacement()
    {
        var trailFactory = new FakeFactory(
            "opentrail",
            "trail_provider",
            context => new FakeProvider(
                context,
                CreateRules("opentrail", "trail_provider"),
                closeAction: () => throw new InvalidOperationException("sensitive-close-detail")));
        var displayFactory = CreateFactory("opengauge", "display_provider");
        var controller = new LoaderSessionController(
            new ILoaderProductProviderFactory[] { trailFactory, displayFactory });
        Require(controller.SelectProduct("opentrail"), "throw-close setup");
        Require(controller.SelectProduct("opengauge"), "throw-close switch");
        Require(trailFactory.LastProvider?.CloseCount == 1, "throw-close one attempt");
        Require(displayFactory.OpenCount == 0, "replacement not opened");
        Require(
            ReferenceEquals(controller.Snapshot.Product, LoaderProductCatalog.Display),
            "requested selection retained");
        Require(!controller.Snapshot.ProviderActive, "replacement provider absent");
        Require(
            !controller.Snapshot.Status.Contains(
                "sensitive-close-detail",
                StringComparison.Ordinal),
            "close exception sanitized");
    }

    private static void FailedFactoriesRemainProviderless()
    {
        var nullFactory = new FakeFactory(
            "opentrail",
            "trail_provider",
            _ => null!);
        var first = new LoaderSessionController(new[] { nullFactory });
        Require(first.SelectProduct("opentrail"), "null factory selection");
        Require(!first.Snapshot.ProviderActive, "null factory inactive");

        var throwingFactory = new FakeFactory(
            "opengauge",
            "display_provider",
            _ => throw new InvalidOperationException("sensitive-open-detail"));
        var second = new LoaderSessionController(new[] { throwingFactory });
        Require(second.SelectProduct("opengauge"), "throwing factory selection");
        Require(!second.Snapshot.ProviderActive, "throwing factory inactive");
        Require(
            !second.Snapshot.Status.Contains(
                "sensitive-open-detail",
                StringComparison.Ordinal),
            "open exception sanitized");
    }

    private static void MismatchedProviderIsRejectedAndClosed()
    {
        var factory = new FakeFactory(
            "opentrail",
            "trail_provider",
            context => new FakeProvider(
                context,
                CreateRules("opengauge", "trail_provider")));
        var controller = new LoaderSessionController(new[] { factory });
        Require(controller.SelectProduct("opentrail"), "mismatch selection");
        Require(!controller.Snapshot.ProviderActive, "mismatch inactive");
        Require(factory.LastProvider?.CloseCount == 1, "mismatch closed once");
        Require(
            !controller.TryCreateOfflineBundleInspectionContext(out _),
            "mismatch context denied");
    }

    private static void ReentrantCloseOperationsAreSuppressed()
    {
        LoaderSessionController? controller = null;
        LoaderBundleInspectionContext? currentContext = null;
        FirmwareBundleCandidateResult? currentResult = null;
        var reentrantSelect = true;
        var reentrantContext = true;
        var reentrantPublication = true;
        var trailFactory = new FakeFactory(
            "opentrail",
            "trail_provider",
            context => new FakeProvider(
                context,
                CreateRules("opentrail", "trail_provider"),
                () =>
                {
                    reentrantSelect = controller!.SelectProduct("opengauge");
                    reentrantContext =
                        controller.TryCreateOfflineBundleInspectionContext(out _);
                    reentrantPublication =
                        currentContext is not null &&
                        currentResult is not null &&
                        controller.CanPublishOfflineBundleInspection(
                            currentContext,
                            currentResult);
                    controller.ReturnToProductChoice();
                }));
        var displayFactory = CreateFactory("opengauge", "display_provider");
        controller = new LoaderSessionController(
            new ILoaderProductProviderFactory[] { trailFactory, displayFactory });
        Require(controller.SelectProduct("opentrail"), "reentrant setup");
        Require(
            controller.TryCreateOfflineBundleInspectionContext(out currentContext),
            "reentrant current context");
        using var bundle = CreateCandidateBundle(
            "opentrail",
            "heltec_v4_bench",
            new byte[] { 7, 8, 9 });
        currentResult =
            FirmwareBundleCandidateInspector.Inspect(bundle, currentContext!);
        Require(controller.SelectProduct("opengauge"), "reentrant switch");
        Require(!reentrantSelect, "reentrant select denied");
        Require(!reentrantContext, "reentrant context denied");
        Require(!reentrantPublication, "reentrant publication denied");
        Require(controller.Snapshot.ProviderActive, "replacement remains active");
        Require(
            ReferenceEquals(controller.Snapshot.Product, LoaderProductCatalog.Display),
            "replacement selection remains");
    }
    private static void ReentrantOwnerDisposalAbortsActivation()
    {
        LoaderSessionController? controller = null;
        FakeProvider? candidate = null;
        var factory = new FakeFactory(
            "opentrail",
            "trail_provider",
            context =>
            {
                candidate = new FakeProvider(
                    context,
                    CreateRules("opentrail", "trail_provider"));
                controller!.Dispose();
                return candidate;
            });
        controller = new LoaderSessionController(new[] { factory });
        Require(!controller.SelectProduct("opentrail"), "disposed activation result");
        Require(candidate?.CloseCount == 1, "disposed candidate close");
        Require(!controller.Snapshot.HasProduct, "disposed selection cleared");
        Require(!controller.Snapshot.ProviderActive, "disposed provider absent");
    }

    private static void StaleProviderResultCannotPublish()
    {
        var trailFactory = CreateFactory("opentrail", "trail_provider");
        var displayFactory = CreateFactory("opengauge", "display_provider");
        var controller = new LoaderSessionController(
            new ILoaderProductProviderFactory[] { trailFactory, displayFactory });
        Require(controller.SelectProduct("opentrail"), "stale setup");
        Require(
            controller.TryCreateOfflineBundleInspectionContext(out var context),
            "stale context");
        using var bundle = CreateCandidateBundle(
            "opentrail",
            "heltec_v4_bench",
            new byte[] { 1, 2, 3 });
        var result = FirmwareBundleCandidateInspector.Inspect(bundle, context!);
        Require(
            controller.CanPublishOfflineBundleInspection(context!, result),
            "current provider publication");
        Require(controller.SelectProduct("opengauge"), "stale switch");
        Require(
            !controller.CanPublishOfflineBundleInspection(context!, result),
            "stale provider publication");
        controller.Dispose();
        Require(
            !controller.CanPublishOfflineBundleInspection(context!, result),
            "disposed provider publication");
    }

    private static void TargetRulesRequireExactAllowedTarget()
    {
        var controller = CreateController("opentrail");
        Require(controller.SelectProduct("opentrail"), "target setup");
        Require(
            controller.TryCreateOfflineBundleInspectionContext(out var context),
            "target context");

        using var allowedBundle = CreateCandidateBundle(
            "opentrail",
            "heltec_v4_bench",
            new byte[] { 4, 5, 6 });
        var allowed = FirmwareBundleCandidateInspector.Inspect(allowedBundle, context!);
        Require(
            controller.CanPublishOfflineBundleInspection(context!, allowed),
            "allowed target");

        using var caseBundle = CreateCandidateBundle(
            "opentrail",
            "Heltec_v4_bench",
            new byte[] { 4, 5, 7 });
        ExpectInvalidData(
            () => FirmwareBundleCandidateInspector.Inspect(caseBundle, context!),
            "case-variant target");

        using var unknownBundle = CreateCandidateBundle(
            "opentrail",
            "other_target",
            new byte[] { 4, 5, 8 });
        var unknown = FirmwareBundleCandidateInspector.Inspect(unknownBundle, context!);
        Require(
            !controller.CanPublishOfflineBundleInspection(context!, unknown),
            "unknown target blocked");
    }

    private static void SignerTrustStaysIndependentFromAdmission()
    {
        var factory = CreateFactory("opentrail", "trail_provider");
        var trust = CreateTrust("opentrail", "trail_provider");
        var controller = new LoaderSessionController(new[] { factory }, new[] { trust });
        Require(controller.SelectProduct("opentrail"), "trust setup");
        Require(
            controller.Snapshot.SignerTrustSourceRevision == TrustRevision,
            "trust revision active");
        Require(
            controller.TryCreateOfflineBundleInspectionContext(out var context),
            "trust context");
        using var bundle = CreateCandidateBundle(
            "opentrail",
            "heltec_v4_bench",
            new byte[] { 9, 8, 7 });
        var result = FirmwareBundleCandidateInspector.Inspect(bundle, context!);
        Require(!result.SignerTrusted, "trust does not verify signer");
        Require(!result.AdmissionAllowed, "trust does not allow admission");
        Require(
            controller.CanPublishOfflineBundleInspection(context!, result),
            "untrusted result remains publishable as inspection");
        Require(!controller.Snapshot.ConnectedDeviceInspectionAvailable, "trust does not enable device inspection");
        Require(!controller.Snapshot.FirmwareBundleSelectionAvailable, "trust does not enable bundle selection");
        Require(!controller.Snapshot.DeviceBundleMatchAvailable, "trust does not enable device match");
        Require(!controller.Snapshot.FirmwareWritingAvailable, "trust does not enable write");
        Require(!controller.Snapshot.RecoveryAvailable, "trust does not enable recovery");
        Require(context!.ProviderKey == "trail_provider", "trust context provider");
        Require(context.ProviderContractVersion == LoaderProviderLifecycle.CurrentContractVersion, "trust context version");
        Require(context.ProviderGeneration == 1, "trust context generation");
        Require(context.TargetRulesSourceRevision == RulesRevision, "trust context rules revision");
        Require(context.SignerTrustSourceRevision == TrustRevision, "trust context policy revision");
    }

    private static void RuleAndTrustInputsAreImmutableCopies()
    {
        var targets = new List<LoaderTargetRule>
        {
            new("heltec_v4_bench", ManifestRevision),
        };
        var rules = new LoaderTargetRuleSet(
            "opentrail",
            "trail_provider",
            LoaderProviderLifecycle.CurrentContractVersion,
            RulesRevision,
            targets);
        targets.Clear();
        Require(rules.Targets.Count == 1, "rules copied");

        var signers = new List<LoaderTrustedSigner>
        {
            new("0123456789abcdef", PublicKeyRevision),
        };
        var trust = new LoaderSignerTrustPolicy(
            "opentrail",
            "trail_provider",
            LoaderProviderLifecycle.CurrentContractVersion,
            TrustRevision,
            signers);
        signers.Clear();
        Require(trust.TrustedSigners.Count == 1, "trust copied");
    }

    private static void TargetRuleValidationRejectsUnsafeIdentity()
    {
        ExpectArgument(
            () => new LoaderTargetRuleSet(
                "opentrail",
                "trail_provider",
                LoaderProviderLifecycle.CurrentContractVersion,
                RulesRevision,
                new[] { new LoaderTargetRule("../private", ManifestRevision) }),
            "unsafe target key");
        ExpectArgument(
            () => new LoaderTargetRuleSet(
                "opentrail",
                "trail_provider",
                LoaderProviderLifecycle.CurrentContractVersion,
                RulesRevision,
                new[] { new LoaderTargetRule("target-with-hyphen", ManifestRevision) }),
            "hyphenated target key");
        ExpectArgument(
            () => new LoaderTargetRuleSet(
                "opentrail",
                "trail_provider",
                LoaderProviderLifecycle.CurrentContractVersion,
                RulesRevision,
                new[]
                {
                    new LoaderTargetRule("duplicate", ManifestRevision),
                    new LoaderTargetRule("duplicate", ManifestRevision),
                }),
            "duplicate target key");
        ExpectArgument(
            () => new LoaderTargetRuleSet(
                "opentrail",
                "trail_provider",
                LoaderProviderLifecycle.CurrentContractVersion,
                new string('A', 64),
                new[] { new LoaderTargetRule("target", ManifestRevision) }),
            "uppercase source revision");
    }

    private static void ProviderSurfaceContainsNoHardwareAuthority()
    {
        var providerMembers = typeof(ILoaderProductProvider)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(member => member.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Require(
            providerMembers.SequenceEqual(
                new[] { "Close", "Context", "TargetRules", "get_Context", "get_TargetRules" },
                StringComparer.Ordinal),
            "provider member whitelist");
        var allNames = string.Join(
            " ",
            typeof(ILoaderProductProvider).Assembly
                .GetTypes()
                .Where(type =>
                    type == typeof(ILoaderProductProvider) ||
                    type == typeof(ILoaderProductProviderFactory))
                .SelectMany(type => type.GetMembers())
                .Select(member => member.Name));
        foreach (var forbidden in new[]
        {
            "Connect", "Enumerate", "Write", "Erase", "Reset", "Reboot", "Recover",
            "SignerTrusted", "AdmissionAllowed",
        })
        {
            Require(
                !allNames.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                "provider forbidden member " + forbidden);
        }
    }

    private static void ProviderFailuresExposeOnlySanitizedStatus()
    {
        var factory = new FakeFactory(
            "opentrail",
            "trail_provider",
            _ => throw new InvalidOperationException(
                "sensitive-provider-detail"));
        var controller = new LoaderSessionController(new[] { factory });
        Require(controller.SelectProduct("opentrail"), "sanitized setup");
        var status = controller.Snapshot.Status;
        Require(!status.Contains("private", StringComparison.OrdinalIgnoreCase), "private text hidden");
        Require(status == "The configured provider could not open. Inspection remains unavailable.", "generic provider failure status");
        Require(!status.Contains("sensitive-provider-detail", StringComparison.Ordinal), "provider detail hidden");
        Require(!status.Contains("C:\\", StringComparison.Ordinal), "path hidden");
    }

    private static FakeFactory CreateFactory(string productKey, string providerKey) =>
        new(
            productKey,
            providerKey,
            context => new FakeProvider(
                context,
                CreateRules(productKey, providerKey)));

    private static LoaderTargetRuleSet CreateRules(
        string productKey,
        string providerKey)
    {
        var targetKey = productKey == "opentrail"
            ? "heltec_v4_bench"
            : "display_reference_target";
        return new LoaderTargetRuleSet(
            productKey,
            providerKey,
            LoaderProviderLifecycle.CurrentContractVersion,
            RulesRevision,
            new[] { new LoaderTargetRule(targetKey, ManifestRevision) });
    }

    private static LoaderSignerTrustPolicy CreateTrust(
        string productKey,
        string providerKey) =>
        new(
            productKey,
            providerKey,
            LoaderProviderLifecycle.CurrentContractVersion,
            TrustRevision,
            new[]
            {
                new LoaderTrustedSigner(
                    "0123456789abcdef",
                    PublicKeyRevision),
            });

    private static MemoryStream CreateCandidateBundle(
        string productKey,
        string targetKey,
        byte[] image)
    {
        var digest = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        var manifest = FirmwareBundleCandidateInspector.SerializeCanonicalManifest(
            productKey,
            targetKey,
            releaseGeneration: 1,
            imageBytes: checked((uint)image.Length),
            imageSha256: digest,
            signerId: "0123456789abcdef");
        var output = new MemoryStream();
        using (var archive = new ZipArchive(
            output,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            WriteEntry(archive, "manifest.json", manifest);
            WriteEntry(archive, "image.bin", image);
            WriteEntry(
                archive,
                "manifest.sig",
                Enumerable.Repeat(
                    (byte)0x5a,
                    FirmwareBundleCandidateInspector.SignatureBytes).ToArray());
        }
        output.Position = 0;
        return output;
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static void ExpectArgument(Action action, string name)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException(
            "FAILED: expected ArgumentException for " + name);
    }

    private static void ExpectInvalidData(Action action, string name)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException(
            "FAILED: expected InvalidDataException for " + name);
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException("FAILED: " + name);
        }
    }

    private sealed class MutableIdentityFactory : ILoaderProductProviderFactory
    {
        private bool rejectIdentityReads;

        public string ProductKey
        {
            get
            {
                ProductKeyReads++;
                if (rejectIdentityReads)
                {
                    throw new InvalidOperationException("late product identity read");
                }
                return "opentrail";
            }
        }

        public string ProviderKey
        {
            get
            {
                ProviderKeyReads++;
                if (rejectIdentityReads)
                {
                    throw new InvalidOperationException("late provider identity read");
                }
                return "trail_provider";
            }
        }

        public uint ProviderContractVersion
        {
            get
            {
                ContractVersionReads++;
                if (rejectIdentityReads)
                {
                    throw new InvalidOperationException("late version identity read");
                }
                return LoaderProviderLifecycle.CurrentContractVersion;
            }
        }

        internal int ProductKeyReads { get; private set; }

        internal int ProviderKeyReads { get; private set; }

        internal int ContractVersionReads { get; private set; }

        internal int OpenCount { get; private set; }

        internal void RejectIdentityReads()
        {
            rejectIdentityReads = true;
        }

        public ILoaderProductProvider Open(LoaderProviderOpenContext context)
        {
            OpenCount++;
            return new FakeProvider(
                context,
                CreateRules("opentrail", "trail_provider"));
        }
    }

    private sealed class FakeFactory : ILoaderProductProviderFactory
    {
        private readonly Func<LoaderProviderOpenContext, ILoaderProductProvider>
            open;

        internal FakeFactory(
            string productKey,
            string providerKey,
            Func<LoaderProviderOpenContext, ILoaderProductProvider> open,
            uint providerContractVersion =
                LoaderProviderLifecycle.CurrentContractVersion)
        {
            ProductKey = productKey;
            ProviderKey = providerKey;
            ProviderContractVersion = providerContractVersion;
            this.open = open;
        }

        public string ProductKey { get; }

        public string ProviderKey { get; }

        public uint ProviderContractVersion { get; }

        internal int OpenCount { get; private set; }

        internal FakeProvider? LastProvider { get; private set; }

        public ILoaderProductProvider Open(LoaderProviderOpenContext context)
        {
            OpenCount++;
            var provider = open(context);
            LastProvider = provider as FakeProvider;
            return provider;
        }
    }

    private sealed class FakeProvider : ILoaderProductProvider
    {
        private readonly Action? closeAction;

        internal FakeProvider(
            LoaderProviderOpenContext context,
            LoaderTargetRuleSet targetRules,
            Action? closeAction = null)
        {
            Context = context;
            TargetRules = targetRules;
            this.closeAction = closeAction;
        }

        public LoaderProviderOpenContext Context { get; }

        public LoaderTargetRuleSet TargetRules { get; }

        internal int CloseCount { get; private set; }

        public void Close()
        {
            CloseCount++;
            closeAction?.Invoke();
        }
    }
}
