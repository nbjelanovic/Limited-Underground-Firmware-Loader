using System.IO;

namespace LimitedUnderground.FirmwareLoader;

public sealed record OfflineBundleInspectionOutcome(
    bool Published,
    string Heading,
    string Summary,
    string ProductKey,
    string TargetKey,
    string ImageBytes,
    string StructureStatus,
    string ImageDigestStatus,
    string SignatureStatus,
    string SignerTrustStatus,
    string AdmissionStatus);

public static class OfflineBundleInspectionWorkflow
{
    public static OfflineBundleInspectionOutcome Inspect(
        LoaderSessionController controller,
        Stream candidate)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(candidate);

        if (!controller.TryCreateOfflineBundleInspectionContext(out var context) ||
            context is null)
        {
            return Unavailable();
        }

        FirmwareBundleCandidateResult result;
        try
        {
            result = FirmwareBundleCandidateInspector.Inspect(candidate, context);
        }
        catch (InvalidDataException)
        {
            return InvalidCandidate();
        }

        if (!controller.CanPublishOfflineBundleInspection(context, result))
        {
            return BlockedCandidate();
        }

        return new OfflineBundleInspectionOutcome(
            Published: true,
            Heading: "Candidate inspected",
            Summary: "Structure, product binding, target rule, and image SHA-256 passed offline inspection.",
            ProductKey: result.ProductKey,
            TargetKey: result.TargetKey,
            ImageBytes: result.ImageBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StructureStatus: "Verified",
            ImageDigestStatus: "Verified",
            SignatureStatus: "Present — not cryptographically verified",
            SignerTrustStatus: "Not configured",
            AdmissionStatus: "Blocked — inspection does not authorize installation");
    }

    private static OfflineBundleInspectionOutcome Unavailable() =>
        Empty(
            "Inspection unavailable",
            "The selected product does not have an active inspection provider.");

    private static OfflineBundleInspectionOutcome InvalidCandidate() =>
        Empty(
            "Candidate not accepted",
            "The selected file is not a valid bounded firmware candidate archive.");

    private static OfflineBundleInspectionOutcome BlockedCandidate() =>
        Empty(
            "Candidate blocked",
            "The candidate does not match the current product session and target rules.");

    private static OfflineBundleInspectionOutcome Empty(string heading, string summary) =>
        new(
            Published: false,
            Heading: heading,
            Summary: summary,
            ProductKey: "Not published",
            TargetKey: "Not published",
            ImageBytes: "Not published",
            StructureStatus: "Not accepted",
            ImageDigestStatus: "Not accepted",
            SignatureStatus: "Not accepted",
            SignerTrustStatus: "Not configured",
            AdmissionStatus: "Blocked");
}
