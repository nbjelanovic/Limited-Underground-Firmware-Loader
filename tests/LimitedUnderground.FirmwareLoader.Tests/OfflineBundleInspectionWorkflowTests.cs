using LimitedUnderground.FirmwareLoader;
using System.IO.Compression;
using System.Security.Cryptography;

internal static class OfflineBundleInspectionWorkflowTests
{
    internal static IReadOnlyList<(string Name, Action Run)> All { get; } =
        new (string Name, Action Run)[]
        {
            ("offline workflow publishes sanitized Trail result", PublishesSanitizedTrailResult),
            ("offline workflow rejects malformed archive generically", RejectsMalformedArchiveGenerically),
            ("offline workflow blocks unknown target generically", BlocksUnknownTargetGenerically),
            ("offline workflow denies missing provider", DeniesMissingProvider),
            ("offline workflow retains no path surface", RetainsNoPathSurface),
        };

    private static void PublishesSanitizedTrailResult()
    {
        using var controller = ProductionController();
        Require(controller.SelectProduct("opentrail"), "Trail selection");
        using var bundle = CreateCandidateBundle("opentrail", "heltec_v4_bench");
        var outcome = OfflineBundleInspectionWorkflow.Inspect(controller, bundle);
        Require(outcome.Published, "published");
        Require(outcome.ProductKey == "opentrail", "product");
        Require(controller.Snapshot.OfflineBundleInspectionAvailable, "offline inspection available");
        Require(outcome.TargetKey == "heltec_v4_bench", "target");
        Require(outcome.ImageBytes == "5", "bytes");
        Require(outcome.StructureStatus == "Verified", "structure");
        Require(outcome.ImageDigestStatus == "Verified", "digest");
        Require(outcome.SignatureStatus.Contains("not cryptographically verified", StringComparison.Ordinal), "signature boundary");
        Require(outcome.SignerTrustStatus == "Not configured", "trust boundary");
        Require(outcome.AdmissionStatus.StartsWith("Blocked", StringComparison.Ordinal), "admission boundary");
    }

    private static void RejectsMalformedArchiveGenerically()
    {
        using var controller = ProductionController();
        Require(controller.SelectProduct("opentrail"), "Trail selection");
        using var malformed = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var outcome = OfflineBundleInspectionWorkflow.Inspect(controller, malformed);
        Require(!outcome.Published, "not published");
        Require(outcome.Heading == "Candidate not accepted", "generic heading");
        Require(outcome.ProductKey == "Not published", "product withheld");
        Require(outcome.TargetKey == "Not published", "target withheld");
    }

    private static void BlocksUnknownTargetGenerically()
    {
        using var controller = ProductionController();
        Require(controller.SelectProduct("opentrail"), "Trail selection");
        using var bundle = CreateCandidateBundle("opentrail", "unknown_target");
        var outcome = OfflineBundleInspectionWorkflow.Inspect(controller, bundle);
        Require(!outcome.Published, "not published");
        Require(outcome.Heading == "Candidate blocked", "blocked heading");
        Require(outcome.ProductKey == "Not published", "product withheld");
        Require(outcome.TargetKey == "Not published", "target withheld");
    }

    private static void DeniesMissingProvider()
    {
        using var controller = ProductionController();
        Require(controller.SelectProduct("opengauge"), "Display selection");
        using var candidate = new MemoryStream(new byte[] { 1 });
        var outcome = OfflineBundleInspectionWorkflow.Inspect(controller, candidate);
        Require(!outcome.Published, "not published");
        Require(outcome.Heading == "Inspection unavailable", "unavailable heading");
        Require(!controller.Snapshot.OfflineBundleInspectionAvailable, "Display inspection unavailable");
    }

    private static void RetainsNoPathSurface()
    {
        var names = typeof(OfflineBundleInspectionOutcome)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(OfflineBundleInspectionWorkflow).GetMethods().Select(method => method.Name))
            .ToArray();
        Require(!names.Any(name =>
            name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("FileName", StringComparison.OrdinalIgnoreCase)), "path surface");
    }

    private static LoaderSessionController ProductionController() =>
        new(
            LoaderProductionProviders.Factories,
            LoaderProductionProviders.SignerTrustPolicies);

    private static MemoryStream CreateCandidateBundle(string productKey, string targetKey)
    {
        var image = new byte[] { 1, 3, 5, 7, 9 };
        var digest = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        var manifest = FirmwareBundleCandidateInspector.SerializeCanonicalManifest(
            productKey,
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
