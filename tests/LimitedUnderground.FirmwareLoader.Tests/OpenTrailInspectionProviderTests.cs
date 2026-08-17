using LimitedUnderground.FirmwareLoader;
using System.IO.Compression;
using System.Security.Cryptography;

internal static class OpenTrailInspectionProviderTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } =
        new (string Name, Action Run)[]
        {
            ("production registry contains only Trail inspection", ProductionRegistryContainsOnlyTrailInspection),
            ("Trail production provider binds public target contract", TrailProviderBindsPublicTargetContract),
            ("Display remains providerless in production", DisplayRemainsProviderless),
            ("Trail production inspection remains non-admitting", TrailInspectionRemainsNonAdmitting),
            ("Trail production rules reject unknown target", TrailRulesRejectUnknownTarget),
            ("production provider surface stays hardware-free", ProductionProviderSurfaceStaysHardwareFree),
        };

    private static void ProductionRegistryContainsOnlyTrailInspection()
    {
        Require(LoaderProductionProviders.Factories.Count == 1, "factory count");
        Require(LoaderProductionProviders.SignerTrustPolicies.Count == 0, "trust empty");
        var factory = LoaderProductionProviders.Factories[0];
        Require(factory.ProductKey == "opentrail", "product key");
        Require(factory.ProviderKey == "opentrail", "provider key");
        Require(factory.ProviderContractVersion == 1, "contract version");
    }

    private static void TrailProviderBindsPublicTargetContract()
    {
        using var controller = CreateProductionController();
        Require(controller.SelectProduct("opentrail"), "Trail selection");
        var snapshot = controller.Snapshot;
        Require(snapshot.ProviderActive, "provider active");
        Require(snapshot.ProviderKey == "opentrail", "snapshot provider key");
        Require(snapshot.ProviderContractVersion == 1, "snapshot version");
        Require(
            snapshot.TargetRulesSourceRevision ==
                OpenTrailInspectionProviderFactory.TargetContractSha256,
            "rules source revision");
        Require(snapshot.SignerTrustSourceRevision is null, "trust absent");
        Require(controller.TryCreateOfflineBundleInspectionContext(out var context), "context minted");
        Require(context is not null && context.ProductKey == "opentrail", "context product");
    }

    private static void DisplayRemainsProviderless()
    {
        using var controller = CreateProductionController();
        Require(controller.SelectProduct("opengauge"), "Display selection");
        Require(!controller.Snapshot.ProviderActive, "Display provider inactive");
        Require(!controller.TryCreateOfflineBundleInspectionContext(out var context), "Display context denied");
        Require(context is null, "Display context null");
    }

    private static void TrailInspectionRemainsNonAdmitting()
    {
        using var controller = CreateProductionController();
        Require(controller.SelectProduct("opentrail"), "Trail selection");
        Require(controller.TryCreateOfflineBundleInspectionContext(out var context), "context minted");
        using var bundle = CreateCandidateBundle("heltec_v4_bench");
        var result = FirmwareBundleCandidateInspector.Inspect(bundle, context!);
        Require(result.StructureVerified && result.ImageDigestVerified, "candidate inspected");
        Require(result.ProductMatched && result.SignaturePresent, "candidate product/signature");
        Require(!result.SignerTrusted && !result.AdmissionAllowed, "no admission");
        Require(controller.CanPublishOfflineBundleInspection(context!, result), "result publishable");
        Require(!controller.Snapshot.ConnectedDeviceInspectionAvailable, "device inspection false");
        Require(!controller.Snapshot.FirmwareBundleSelectionAvailable, "bundle UI false");
        Require(!controller.Snapshot.DeviceBundleMatchAvailable, "device match false");
        Require(!controller.Snapshot.FirmwareWritingAvailable, "write false");
        Require(!controller.Snapshot.RecoveryAvailable, "recovery false");
    }

    private static void TrailRulesRejectUnknownTarget()
    {
        using var controller = CreateProductionController();
        Require(controller.SelectProduct("opentrail"), "Trail selection");
        Require(controller.TryCreateOfflineBundleInspectionContext(out var context), "context minted");
        using var bundle = CreateCandidateBundle("unknown_target");
        var result = FirmwareBundleCandidateInspector.Inspect(bundle, context!);
        Require(!controller.CanPublishOfflineBundleInspection(context!, result), "unknown target blocked");
    }

    private static void ProductionProviderSurfaceStaysHardwareFree()
    {
        var members = typeof(OpenTrailInspectionProviderFactory).Assembly
            .GetTypes()
            .Where(type => type.Name.Contains("OpenTrailInspectionProvider", StringComparison.Ordinal))
            .SelectMany(type => type.GetMembers())
            .Select(member => member.Name)
            .ToArray();
        foreach (var prohibited in new[]
        {
            "Device", "Serial", "Bluetooth", "Connect", "Write", "Erase",
            "Reset", "Reboot", "Recovery", "Install",
        })
        {
            Require(!members.Any(name => name.Contains(prohibited, StringComparison.OrdinalIgnoreCase)), prohibited);
        }
    }

    private static LoaderSessionController CreateProductionController() =>
        new(
            LoaderProductionProviders.Factories,
            LoaderProductionProviders.SignerTrustPolicies);

    private static MemoryStream CreateCandidateBundle(string targetKey)
    {
        var image = new byte[] { 1, 4, 9, 16 };
        var digest = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        var manifest = FirmwareBundleCandidateInspector.SerializeCanonicalManifest(
            "opentrail",
            targetKey,
            releaseGeneration: 1,
            imageBytes: checked((uint)image.Length),
            imageSha256: digest,
            signerId: "0123456789abcdef");
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "manifest.json", manifest);
            WriteEntry(archive, "image.bin", image);
            WriteEntry(
                archive,
                "manifest.sig",
                Enumerable.Repeat((byte)0x5a, FirmwareBundleCandidateInspector.SignatureBytes).ToArray());
        }
        output.Position = 0;
        return output;
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
