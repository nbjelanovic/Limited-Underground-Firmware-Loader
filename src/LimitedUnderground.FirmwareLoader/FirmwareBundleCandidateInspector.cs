using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace LimitedUnderground.FirmwareLoader;

public sealed class FirmwareBundleCandidateResult
{
    internal FirmwareBundleCandidateResult(
        bool structureVerified,
        bool imageDigestVerified,
        bool productMatched,
        bool signaturePresent,
        bool signerTrusted,
        bool admissionAllowed,
        LoaderBundleInspectionContext context,
        string productKey,
        string targetKey,
        ulong releaseGeneration,
        uint imageBytes,
        string summary,
        string blockerText)
    {
        StructureVerified = structureVerified;
        ImageDigestVerified = imageDigestVerified;
        ProductMatched = productMatched;
        SignaturePresent = signaturePresent;
        SignerTrusted = signerTrusted;
        AdmissionAllowed = admissionAllowed;
        Context = context;
        SessionRevision = context.SessionRevision;
        ProductKey = productKey;
        TargetKey = targetKey;
        ReleaseGeneration = releaseGeneration;
        ImageBytes = imageBytes;
        Summary = summary;
        BlockerText = blockerText;
    }

    public bool StructureVerified { get; }

    public bool ImageDigestVerified { get; }

    public bool ProductMatched { get; }

    public bool SignaturePresent { get; }

    public bool SignerTrusted { get; }

    public bool AdmissionAllowed { get; }

    public ulong SessionRevision { get; }

    public string ProductKey { get; }

    public string TargetKey { get; }

    public ulong ReleaseGeneration { get; }

    public uint ImageBytes { get; }

    public string Summary { get; }

    public string BlockerText { get; }

    internal LoaderBundleInspectionContext Context { get; }
}

public static class FirmwareBundleCandidateInspector
{
    public const string Schema = "firmware_bundle_candidate_v1";
    public const string SignatureAlgorithm = "rsa_pss_3072_sha256";
    public const int MaximumArchiveBytes = 20 * 1024 * 1024;
    public const int MaximumManifestBytes = 4096;
    public const int MaximumImageBytes = 16 * 1024 * 1024;
    public const int SignatureBytes = 384;

    private const string ManifestEntryName = "manifest.json";
    private const string ImageEntryName = "image.bin";
    private const string SignatureEntryName = "manifest.sig";

    private static readonly string[] CanonicalPropertyNames =
    [
        "schema",
        "product_key",
        "target_key",
        "release_generation",
        "image_bytes",
        "image_sha256",
        "signer_id",
        "signature_algorithm",
    ];

    public static FirmwareBundleCandidateResult Inspect(
        Stream input,
        LoaderBundleInspectionContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        if (!input.CanRead || !input.CanSeek)
        {
            throw new InvalidDataException("Candidate bundle input must be readable and seekable.");
        }

        var originalPosition = input.Position;
        try
        {
            input.Position = 0;
            return InspectAtStart(input, context);
        }
        finally
        {
            input.Position = originalPosition;
        }
    }

    private static FirmwareBundleCandidateResult InspectAtStart(
        Stream input,
        LoaderBundleInspectionContext context)
    {
        if (input.Length <= 0 || input.Length > MaximumArchiveBytes)
        {
            throw new InvalidDataException("Candidate bundle size is outside the inspection limit.");
        }
        if (LoaderProductCatalog.Find(context.ProductKey) is null ||
            context.SessionRevision == 0)
        {
            throw new InvalidDataException("Selected product context is not accepted.");
        }

        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count != 3)
        {
            throw new InvalidDataException("Candidate bundle must contain exactly three entries.");
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (!entries.TryAdd(entry.FullName, entry) ||
                entry.FullName is not (ManifestEntryName or ImageEntryName or SignatureEntryName))
            {
                throw new InvalidDataException("Candidate bundle entry set is not accepted.");
            }
        }

        var manifestBytes = ReadBoundedEntry(
            entries[ManifestEntryName],
            minimumBytes: 2,
            maximumBytes: MaximumManifestBytes,
            label: "manifest");
        var manifest = ParseCanonicalManifest(manifestBytes);

        var imageBytes = ReadBoundedEntry(
            entries[ImageEntryName],
            minimumBytes: 1,
            maximumBytes: MaximumImageBytes,
            label: "image");
        if (imageBytes.Length != manifest.ImageBytes)
        {
            throw new InvalidDataException("Candidate image length does not match its manifest.");
        }

        var imageDigest = SHA256.HashData(imageBytes);
        if (!CryptographicOperations.FixedTimeEquals(imageDigest, manifest.ImageSha256))
        {
            throw new InvalidDataException("Candidate image digest does not match its manifest.");
        }

        var signature = ReadBoundedEntry(
            entries[SignatureEntryName],
            minimumBytes: SignatureBytes,
            maximumBytes: SignatureBytes,
            label: "signature");
        if (signature.All(static value => value == 0))
        {
            throw new InvalidDataException("Candidate signature is empty.");
        }

        var productMatched = string.Equals(
            manifest.ProductKey,
            context.ProductKey,
            StringComparison.Ordinal);

        return new FirmwareBundleCandidateResult(
            structureVerified: true,
            imageDigestVerified: true,
            productMatched: productMatched,
            signaturePresent: true,
            signerTrusted: false,
            admissionAllowed: false,
            context: context,
            productKey: manifest.ProductKey,
            targetKey: manifest.TargetKey,
            releaseGeneration: manifest.ReleaseGeneration,
            imageBytes: manifest.ImageBytes,
            summary: productMatched
                ? "Candidate structure, product binding, and image SHA-256 verified"
                : "Candidate belongs to a different Limited Underground system",
            blockerText: productMatched
                ? "BLOCKED: No trusted release signer or target provider is configured."
                : "BLOCKED: Candidate product does not match the selected system.");
    }

    public static byte[] SerializeCanonicalManifest(
        string productKey,
        string targetKey,
        ulong releaseGeneration,
        uint imageBytes,
        string imageSha256,
        string signerId)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            output,
            new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", Schema);
            writer.WriteString("product_key", productKey);
            writer.WriteString("target_key", targetKey);
            writer.WriteNumber("release_generation", releaseGeneration);
            writer.WriteNumber("image_bytes", imageBytes);
            writer.WriteString("image_sha256", imageSha256);
            writer.WriteString("signer_id", signerId);
            writer.WriteString("signature_algorithm", SignatureAlgorithm);
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static CandidateManifest ParseCanonicalManifest(byte[] bytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 3,
                });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Candidate manifest is not valid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.EnumerateObject().Select(static property => property.Name)
                    .SequenceEqual(CanonicalPropertyNames, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Candidate manifest is not canonical.");
            }

            var schema = RequiredString(root, "schema", 64);
            var productKey = RequiredString(root, "product_key", 24);
            var targetKey = RequiredString(root, "target_key", 64);
            var releaseGeneration = RequiredUInt64(root, "release_generation");
            var imageBytes = RequiredUInt32(root, "image_bytes");
            var imageSha256Text = RequiredString(root, "image_sha256", 64);
            var signerId = RequiredString(root, "signer_id", 16);
            var signatureAlgorithm = RequiredString(root, "signature_algorithm", 32);

            if (schema != Schema ||
                LoaderProductCatalog.Find(productKey) is null ||
                !IsEngineeringKey(targetKey, 64) ||
                releaseGeneration == 0 ||
                imageBytes == 0 ||
                imageBytes > MaximumImageBytes ||
                !IsLowerHex(imageSha256Text, 64) ||
                !IsLowerHex(signerId, 16) ||
                signerId.All(static value => value == '0') ||
                signatureAlgorithm != SignatureAlgorithm)
            {
                throw new InvalidDataException("Candidate manifest fields are not accepted.");
            }

            var canonical = SerializeCanonicalManifest(
                productKey,
                targetKey,
                releaseGeneration,
                imageBytes,
                imageSha256Text,
                signerId);
            if (!bytes.AsSpan().SequenceEqual(canonical))
            {
                throw new InvalidDataException("Candidate manifest is not canonically encoded.");
            }

            return new CandidateManifest(
                productKey,
                targetKey,
                releaseGeneration,
                imageBytes,
                Convert.FromHexString(imageSha256Text));
        }
    }

    private static byte[] ReadBoundedEntry(
        ZipArchiveEntry entry,
        int minimumBytes,
        int maximumBytes,
        string label)
    {
        if (entry.Length < minimumBytes || entry.Length > maximumBytes)
        {
            throw new InvalidDataException($"Candidate {label} size is outside its inspection limit.");
        }

        using var input = entry.Open();
        using var output = new MemoryStream((int)Math.Min(entry.Length, maximumBytes));
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var remaining = maximumBytes + 1L - output.Length;
            if (remaining <= 0)
            {
                throw new InvalidDataException($"Candidate {label} expands beyond its inspection limit.");
            }

            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                break;
            }
            output.Write(buffer, 0, read);
        }

        if (output.Length < minimumBytes ||
            output.Length > maximumBytes ||
            output.Length != entry.Length)
        {
            throw new InvalidDataException($"Candidate {label} expanded size is not accepted.");
        }
        return output.ToArray();
    }

    private static string RequiredString(JsonElement root, string name, int maximumLength)
    {
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Candidate manifest field type is not accepted.");
        }

        var value = element.GetString() ?? string.Empty;
        if (value.Length == 0 || value.Length > maximumLength)
        {
            throw new InvalidDataException("Candidate manifest text is outside its limit.");
        }
        return value;
    }

    private static uint RequiredUInt32(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        if (!element.TryGetUInt32(out var value))
        {
            throw new InvalidDataException("Candidate manifest integer is outside its limit.");
        }
        return value;
    }

    private static ulong RequiredUInt64(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        if (!element.TryGetUInt64(out var value))
        {
            throw new InvalidDataException("Candidate manifest integer is outside its limit.");
        }
        return value;
    }

    private static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsEngineeringKey(string value, int maximumLength) =>
        value.Length <= maximumLength &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private sealed record CandidateManifest(
        string ProductKey,
        string TargetKey,
        ulong ReleaseGeneration,
        uint ImageBytes,
        byte[] ImageSha256);
}
