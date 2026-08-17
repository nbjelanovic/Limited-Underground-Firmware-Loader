using LimitedUnderground.FirmwareLoader;
using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;
using System.Security.Cryptography;

var repositoryRoot = args.Length == 1
    ? Path.GetFullPath(args[0])
    : throw new InvalidOperationException("Repository root argument is required.");

var tests = new (string Name, Action Run)[]
{
    ("catalog has exact two systems", CatalogHasExactTwoSystems),
    ("public names are exact", PublicNamesAreExact),
    ("engineering keys stay stable", EngineeringKeysStayStable),
    ("initial session is unselected", InitialSessionIsUnselected),
    ("Trail selection is exact", TrailSelectionIsExact),
    ("Display selection replaces Trail", DisplaySelectionReplacesTrail),
    ("exact reselection is a no-op", ExactReselectionIsNoOp),
    ("unknown selection fails without mutation", UnknownSelectionFailsWithoutMutation),
    ("return invalidates selected revision", ReturnInvalidatesRevision),
    ("all operational capabilities remain false", AllOperationalCapabilitiesRemainFalse),
    ("UI exposes chooser and disabled operation", UiExposesChooserAndDisabledOperation),
    ("source contains no hardware mutation adapter", SourceContainsNoHardwareMutationAdapter),
    ("Trail candidate is inspected without admission", TrailCandidateIsInspectedWithoutAdmission),
    ("cross-product candidate is blocked", CrossProductCandidateIsBlocked),
    ("candidate image digest mismatch is rejected", CandidateImageDigestMismatchIsRejected),
    ("noncanonical manifest is rejected", NoncanonicalManifestIsRejected),
    ("unexpected archive entry is rejected", UnexpectedArchiveEntryIsRejected),
    ("empty signature is rejected", EmptySignatureIsRejected),
    ("unknown manifest product is rejected", UnknownManifestProductIsRejected),
    ("Display candidate uses Display session", DisplayCandidateUsesDisplaySession),
    ("no selection cannot mint inspection context", NoSelectionCannotMintInspectionContext),
    ("exact reselection keeps inspection context current", ExactReselectionKeepsContextCurrent),
    ("product switch invalidates inspection context", ProductSwitchInvalidatesContext),
    ("back to chooser invalidates inspection context", BackToChooserInvalidatesContext),
    ("stream position restores after failure", StreamPositionRestoresAfterFailure),
    ("maximum compressed image boundary is accepted", MaximumCompressedImageBoundaryIsAccepted),
    ("compressed maximum plus one is rejected", CompressedMaximumPlusOneIsRejected),
    ("forged central-directory size is rejected", ForgedCentralDirectorySizeIsRejected),
    ("oversized manifest and signature are rejected", OversizedManifestAndSignatureAreRejected),
    ("inspection authority cannot be fabricated", InspectionAuthorityCannotBeFabricated),
    ("cross-controller result reuse is rejected", CrossControllerResultReuseIsRejected),
}
.Concat(ProviderLifecycleTests.All)
.ToArray();

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

Console.WriteLine($"{tests.Length} shared loader offline groups passed.");

void CatalogHasExactTwoSystems()
{
    Require(LoaderProductCatalog.All.Count == 2, "catalog count");
    Require(ReferenceEquals(LoaderProductCatalog.Find("opentrail"), LoaderProductCatalog.Trail), "Trail lookup");
    Require(ReferenceEquals(LoaderProductCatalog.Find("opengauge"), LoaderProductCatalog.Display), "Display lookup");
}

void PublicNamesAreExact()
{
    Require(LoaderProductCatalog.ParentName == "Limited Underground", "parent name");
    Require(LoaderProductCatalog.WindowTitle == "Limited Underground Firmware Loader — Preview", "window title");
    Require(LoaderProductCatalog.Trail.DisplayName == "Limited Underground Trail", "Trail name");
    Require(LoaderProductCatalog.Display.DisplayName == "Limited Underground Display", "Display name");
}

void EngineeringKeysStayStable()
{
    Require(LoaderProductCatalog.Trail.EngineeringKey == "opentrail", "Trail key");
    Require(LoaderProductCatalog.Display.EngineeringKey == "opengauge", "Display key");
    Require(!LoaderProductCatalog.All.Any(product => product.EngineeringKey.Contains(' ')), "keys contain no spaces");
}

void InitialSessionIsUnselected()
{
    var snapshot = new LoaderSessionController().Snapshot;
    Require(snapshot.Revision == 0 && !snapshot.HasProduct, "initial state");
    Require(!snapshot.FirmwareWritingAvailable && !snapshot.RecoveryAvailable, "initial authority");
}

void TrailSelectionIsExact()
{
    var controller = new LoaderSessionController();
    Require(controller.SelectProduct("opentrail"), "Trail select result");
    Require(controller.Snapshot.Revision == 1, "Trail revision");
    Require(ReferenceEquals(controller.Snapshot.Product, LoaderProductCatalog.Trail), "Trail product");
}

void DisplaySelectionReplacesTrail()
{
    var controller = new LoaderSessionController();
    Require(controller.SelectProduct("opentrail"), "Trail prerequisite");
    Require(controller.SelectProduct("opengauge"), "Display select result");
    Require(controller.Snapshot.Revision == 2, "switch revision");
    Require(ReferenceEquals(controller.Snapshot.Product, LoaderProductCatalog.Display), "Display product");
}

void ExactReselectionIsNoOp()
{
    var controller = new LoaderSessionController();
    Require(controller.SelectProduct("opentrail"), "initial select");
    var revision = controller.Snapshot.Revision;
    Require(controller.SelectProduct("opentrail"), "reselect result");
    Require(controller.Snapshot.Revision == revision, "reselect revision");
}

void UnknownSelectionFailsWithoutMutation()
{
    var controller = new LoaderSessionController();
    Require(controller.SelectProduct("opengauge"), "initial Display select");
    var before = controller.Snapshot;
    Require(!controller.SelectProduct("unknown"), "unknown select result");
    Require(controller.Snapshot == before, "unknown selection mutation");
}

void ReturnInvalidatesRevision()
{
    var controller = new LoaderSessionController();
    Require(controller.SelectProduct("opentrail"), "initial select");
    controller.ReturnToProductChoice();
    Require(controller.Snapshot.Revision == 2 && !controller.Snapshot.HasProduct, "return state");
    controller.ReturnToProductChoice();
    Require(controller.Snapshot.Revision == 2, "empty return no-op");
}

void AllOperationalCapabilitiesRemainFalse()
{
    foreach (var product in LoaderProductCatalog.All)
    {
        var controller = new LoaderSessionController();
        Require(controller.SelectProduct(product.EngineeringKey), "select " + product.EngineeringKey);
        var snapshot = controller.Snapshot;
        Require(!snapshot.ConnectedDeviceInspectionAvailable, "device inspection " + product.EngineeringKey);
        Require(!snapshot.FirmwareBundleSelectionAvailable, "bundle selection " + product.EngineeringKey);
        Require(!snapshot.DeviceBundleMatchAvailable, "device match " + product.EngineeringKey);
        Require(!snapshot.FirmwareWritingAvailable, "firmware write " + product.EngineeringKey);
        Require(!snapshot.RecoveryAvailable, "recovery " + product.EngineeringKey);
    }
}

void UiExposesChooserAndDisabledOperation()
{
    var xaml = File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "LimitedUnderground.FirmwareLoader",
        "MainWindow.xaml"));
    Require(xaml.Contains("Limited Underground Trail", StringComparison.Ordinal), "Trail choice copy");
    Require(xaml.Contains("Limited Underground Display", StringComparison.Ordinal), "Display choice copy");
    Require(xaml.Contains("x:Name=\"ContinueButton\"", StringComparison.Ordinal), "continue button");
    Require(xaml.Contains("IsEnabled=\"False\"", StringComparison.Ordinal), "disabled continue");
    Require(xaml.Contains("Firmware installation and recovery are not available", StringComparison.Ordinal), "safety footer");

    var codeBehind = File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "LimitedUnderground.FirmwareLoader",
        "MainWindow.xaml.cs"));
    Require(codeBehind.Contains("session.Dispose();", StringComparison.Ordinal), "window closes session");
}

void SourceContainsNoHardwareMutationAdapter()
{
    var sourceRoot = Path.Combine(repositoryRoot, "src");
    var source = string.Join(
        '\n',
        Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

    var prohibited = new[]
    {
        "System.IO.Ports",
        "SerialPort",
        "Windows.Devices",
        "ManagementObjectSearcher",
        "Process.Start",
        "esptool",
        "write-flash",
        "write_flash",
        "erase-flash",
        "erase_flash",
        "read-flash",
        "read_flash",
    };

    foreach (var token in prohibited)
    {
        Require(!source.Contains(token, StringComparison.OrdinalIgnoreCase), "prohibited source token " + token);
    }
}

void TrailCandidateIsInspectedWithoutAdmission()
{
    var (controller, context) = CreateInspectionSession("opentrail");
    var image = new byte[] { 1, 3, 5, 7, 9 };
    using var bundle = CreateCandidateBundle("opentrail", "heltec_v4_bench", image);
    bundle.Position = 2;
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);

    Require(result.StructureVerified, "Trail bundle structure");
    Require(result.ImageDigestVerified, "Trail bundle digest");
    Require(result.ProductMatched, "Trail product match");
    Require(result.SignaturePresent, "Trail signature presence");
    Require(!result.SignerTrusted && !result.AdmissionAllowed, "Trail admission boundary");
    Require(result.SessionRevision == context.SessionRevision, "Trail session revision");
    Require(result.TargetKey == "heltec_v4_bench", "Trail target key");
    Require(controller.CanPublishOfflineBundleInspection(context, result), "Trail current publication");
    Require(bundle.CanRead && bundle.Position == 2, "successful inspection restores stream");
}

void CrossProductCandidateIsBlocked()
{
    var (controller, context) = CreateInspectionSession("opentrail");
    using var bundle = CreateCandidateBundle(
        "opengauge",
        "display_reference_target",
        new byte[] { 2, 4, 6, 8 });
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);

    Require(!result.ProductMatched, "cross-product match");
    Require(!result.SignerTrusted && !result.AdmissionAllowed, "cross-product authority");
    Require(result.ProductKey == "opengauge", "cross-product identity");
    Require(!controller.CanPublishOfflineBundleInspection(context, result), "cross-product publication");
}

void CandidateImageDigestMismatchIsRejected()
{
    var (_, context) = CreateInspectionSession("opentrail");
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        new byte[] { 8, 6, 4, 2 },
        manifestImage: new byte[] { 1, 3, 5, 7 });
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "digest mismatch");
}

void NoncanonicalManifestIsRejected()
{
    var (_, context) = CreateInspectionSession("opentrail");
    var image = new byte[] { 10, 20, 30 };
    var manifest = CreateManifest("opentrail", "heltec_v4_bench", image)
        .Concat(new byte[] { (byte)' ' })
        .ToArray();
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        image,
        manifestOverride: manifest);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "noncanonical manifest");
}

void UnexpectedArchiveEntryIsRejected()
{
    var (_, context) = CreateInspectionSession("opengauge");
    using var bundle = CreateCandidateBundle(
        "opengauge",
        "display_reference_target",
        new byte[] { 11, 22 },
        addUnexpectedEntry: true);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "unexpected archive entry");
}

void EmptySignatureIsRejected()
{
    var (_, context) = CreateInspectionSession("opentrail");
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        new byte[] { 4, 3, 2, 1 },
        signatureOverride: new byte[FirmwareBundleCandidateInspector.SignatureBytes]);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "empty signature");
}

void UnknownManifestProductIsRejected()
{
    var (_, context) = CreateInspectionSession("opentrail");
    var image = new byte[] { 12, 34, 56 };
    using var bundle = CreateCandidateBundle("unknown", "unknown_target", image);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "unknown manifest product");
}

void DisplayCandidateUsesDisplaySession()
{
    var (controller, context) = CreateInspectionSession("opengauge");
    using var bundle = CreateCandidateBundle(
        "opengauge",
        "display_reference_target",
        new byte[] { 7, 14, 21 });
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);
    Require(result.ProductMatched, "Display match");
    Require(controller.CanPublishOfflineBundleInspection(context, result), "Display current publication");
    Require(!result.AdmissionAllowed, "Display admission boundary");
}

void NoSelectionCannotMintInspectionContext()
{
    var controller = new LoaderSessionController();
    Require(!controller.TryCreateOfflineBundleInspectionContext(out var context), "empty context result");
    Require(context is null, "empty context value");
}

void ExactReselectionKeepsContextCurrent()
{
    var (controller, context) = CreateInspectionSession("opentrail");
    Require(controller.SelectProduct("opentrail"), "exact reselect");
    using var bundle = CreateCandidateBundle("opentrail", "heltec_v4_bench", new byte[] { 1, 2 });
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);
    Require(controller.CanPublishOfflineBundleInspection(context, result), "reselection publication");
}

void ProductSwitchInvalidatesContext()
{
    var (controller, context) = CreateInspectionSession("opentrail");
    using var bundle = CreateCandidateBundle("opentrail", "heltec_v4_bench", new byte[] { 2, 3 });
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);
    Require(controller.SelectProduct("opengauge"), "switch product");
    Require(!controller.CanPublishOfflineBundleInspection(context, result), "switched stale publication");
}

void BackToChooserInvalidatesContext()
{
    var (controller, context) = CreateInspectionSession("opengauge");
    using var bundle = CreateCandidateBundle("opengauge", "display_reference_target", new byte[] { 3, 4 });
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);
    controller.ReturnToProductChoice();
    Require(!controller.CanPublishOfflineBundleInspection(context, result), "chooser stale publication");
}

void StreamPositionRestoresAfterFailure()
{
    var (_, context) = CreateInspectionSession("opentrail");
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        new byte[] { 8, 6, 4, 2 },
        manifestImage: new byte[] { 1, 3, 5, 7 });
    bundle.Position = 3;
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "failure position restore");
    Require(bundle.Position == 3, "failed inspection restores stream");
}

void MaximumCompressedImageBoundaryIsAccepted()
{
    var (_, context) = CreateInspectionSession("opentrail");
    var image = new byte[FirmwareBundleCandidateInspector.MaximumImageBytes];
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        image,
        compressionLevel: CompressionLevel.Optimal);
    Require(bundle.Length < FirmwareBundleCandidateInspector.MaximumArchiveBytes, "compressed boundary archive");
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);
    Require(result.ImageBytes == FirmwareBundleCandidateInspector.MaximumImageBytes, "maximum image bytes");
}

void CompressedMaximumPlusOneIsRejected()
{
    var (_, context) = CreateInspectionSession("opentrail");
    var image = new byte[FirmwareBundleCandidateInspector.MaximumImageBytes + 1];
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        image,
        compressionLevel: CompressionLevel.Optimal);
    Require(bundle.Length < FirmwareBundleCandidateInspector.MaximumArchiveBytes, "compressed oversized archive");
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "compressed maximum plus one");
}

void ForgedCentralDirectorySizeIsRejected()
{
    var (_, context) = CreateInspectionSession("opentrail");
    var image = new byte[] { 1, 2, 3, 4 };
    var imageDigest = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
    var manifest = FirmwareBundleCandidateInspector.SerializeCanonicalManifest(
        "opentrail",
        "heltec_v4_bench",
        releaseGeneration: 1,
        imageBytes: 1,
        imageSha256: imageDigest,
        signerId: "0123456789abcdef");
    using var bundle = CreateCandidateBundle(
        "opentrail",
        "heltec_v4_bench",
        image,
        manifestOverride: manifest);
    RewriteCentralDirectoryUncompressedSize(bundle, "image.bin", 1);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(bundle, context),
        "forged central directory size");
}

void OversizedManifestAndSignatureAreRejected()
{
    var (_, context) = CreateInspectionSession("opengauge");
    using var manifestBundle = CreateCandidateBundle(
        "opengauge",
        "display_reference_target",
        new byte[] { 1 },
        manifestOverride: new byte[FirmwareBundleCandidateInspector.MaximumManifestBytes + 1]);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(manifestBundle, context),
        "oversized manifest");

    using var signatureBundle = CreateCandidateBundle(
        "opengauge",
        "display_reference_target",
        new byte[] { 1 },
        signatureOverride: new byte[FirmwareBundleCandidateInspector.SignatureBytes + 1]);
    ExpectInvalidData(
        () => FirmwareBundleCandidateInspector.Inspect(signatureBundle, context),
        "oversized signature");
}

void InspectionAuthorityCannotBeFabricated()
{
    Require(
        typeof(LoaderBundleInspectionContext).GetConstructors().Length == 0,
        "inspection context public constructor");
    Require(
        typeof(FirmwareBundleCandidateResult).GetConstructors().Length == 0,
        "inspection result public constructor");
}

void CrossControllerResultReuseIsRejected()
{
    var (firstController, context) = CreateInspectionSession("opentrail");
    using var bundle = CreateCandidateBundle("opentrail", "heltec_v4_bench", new byte[] { 5, 6, 7 });
    var result = FirmwareBundleCandidateInspector.Inspect(bundle, context);
    Require(firstController.CanPublishOfflineBundleInspection(context, result), "first controller publication");

    var secondController = ProviderLifecycleTests.CreateController("opentrail");
    Require(secondController.SelectProduct("opentrail"), "second controller select");
    Require(secondController.Snapshot.ProviderActive, "second controller provider active");
    Require(secondController.Snapshot.Revision == context.SessionRevision, "numeric revision collision prerequisite");
    Require(!secondController.CanPublishOfflineBundleInspection(context, result), "cross-controller publication");
}
static (LoaderSessionController Controller, LoaderBundleInspectionContext Context)
    CreateInspectionSession(string productKey)
{
    var controller = ProviderLifecycleTests.CreateController(productKey);
    Require(controller.SelectProduct(productKey), "select inspection product " + productKey);
    Require(controller.TryCreateOfflineBundleInspectionContext(out var context), "create inspection context " + productKey);
    return (controller, context ?? throw new InvalidOperationException("Inspection context missing."));
}
static MemoryStream CreateCandidateBundle(
    string productKey,
    string targetKey,
    byte[] image,
    byte[]? manifestImage = null,
    byte[]? manifestOverride = null,
    byte[]? signatureOverride = null,
    bool addUnexpectedEntry = false,
    CompressionLevel compressionLevel = CompressionLevel.NoCompression)
{
    var output = new MemoryStream();
    using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
    {
        WriteEntry(
            archive,
            "manifest.json",
            manifestOverride ?? CreateManifest(productKey, targetKey, manifestImage ?? image),
            compressionLevel);
        WriteEntry(archive, "image.bin", image, compressionLevel);

        var signature = signatureOverride ??
            Enumerable.Repeat((byte)0x5a, FirmwareBundleCandidateInspector.SignatureBytes).ToArray();
        WriteEntry(archive, "manifest.sig", signature, compressionLevel);
        if (addUnexpectedEntry)
        {
            WriteEntry(archive, "unexpected.txt", new byte[] { 1 }, compressionLevel);
        }
    }
    output.Position = 0;
    return output;
}

static byte[] CreateManifest(string productKey, string targetKey, byte[] image)
{
    var digest = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
    return FirmwareBundleCandidateInspector.SerializeCanonicalManifest(
        productKey,
        targetKey,
        releaseGeneration: 1,
        imageBytes: checked((uint)image.Length),
        imageSha256: digest,
        signerId: "0123456789abcdef");
}

static void WriteEntry(
    ZipArchive archive,
    string name,
    byte[] bytes,
    CompressionLevel compressionLevel)
{
    var entry = archive.CreateEntry(name, compressionLevel);
    using var stream = entry.Open();
    stream.Write(bytes);
}

static void RewriteCentralDirectoryUncompressedSize(
    MemoryStream bundle,
    string entryName,
    uint replacementSize)
{
    var bytes = bundle.ToArray();
    var expectedName = Encoding.UTF8.GetBytes(entryName);
    var matches = 0;

    for (var offset = 0; offset <= bytes.Length - 46; offset++)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) != 0x02014b50)
        {
            continue;
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 30, 2));
        var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 32, 2));
        var recordLength = 46 + nameLength + extraLength + commentLength;
        if (offset + recordLength > bytes.Length)
        {
            throw new InvalidDataException("Test ZIP central-directory record is truncated.");
        }

        if (bytes.AsSpan(offset + 46, nameLength).SequenceEqual(expectedName))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(offset + 24, 4),
                replacementSize);
            matches++;
        }
        offset += recordLength - 1;
    }

    Require(matches == 1, "central-directory entry match");
    bundle.Position = 0;
    bundle.Write(bytes);
    bundle.SetLength(bytes.Length);
    bundle.Position = 0;
}

static void ExpectInvalidData(Action action, string name)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new InvalidOperationException("FAILED: expected InvalidDataException for " + name);
}
static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException("FAILED: " + name);
    }
}
