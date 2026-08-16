namespace LimitedUnderground.FirmwareLoader;

public sealed class LoaderBundleInspectionContext
{
    internal LoaderBundleInspectionContext(
        object ownerToken,
        ulong sessionRevision,
        string productKey)
    {
        OwnerToken = ownerToken;
        SessionRevision = sessionRevision;
        ProductKey = productKey;
    }

    public ulong SessionRevision { get; }

    public string ProductKey { get; }

    internal object OwnerToken { get; }
}

public sealed record LoaderSessionSnapshot(
    ulong Revision,
    LoaderProductFamily? Product,
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

public sealed class LoaderSessionController
{
    private readonly object inspectionOwnerToken = new();
    private ulong revision;
    private LoaderProductFamily? selectedProduct;

    public LoaderSessionSnapshot Snapshot => CreateSnapshot();

    public bool SelectProduct(string engineeringKey)
    {
        var nextProduct = LoaderProductCatalog.Find(engineeringKey);
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

        selectedProduct = nextProduct;
        revision = checked(revision + 1);
        return true;
    }

    public void ReturnToProductChoice()
    {
        if (selectedProduct is null)
        {
            return;
        }

        selectedProduct = null;
        revision = checked(revision + 1);
    }

    public bool TryCreateOfflineBundleInspectionContext(
        out LoaderBundleInspectionContext? context)
    {
        if (selectedProduct is null || revision == 0)
        {
            context = null;
            return false;
        }

        context = new LoaderBundleInspectionContext(
            inspectionOwnerToken,
            revision,
            selectedProduct.EngineeringKey);
        return true;
    }

    public bool CanPublishOfflineBundleInspection(
        LoaderBundleInspectionContext context,
        FirmwareBundleCandidateResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        return selectedProduct is not null &&
            ReferenceEquals(context.OwnerToken, inspectionOwnerToken) &&
            ReferenceEquals(result.Context, context) &&
            context.SessionRevision == revision &&
            result.SessionRevision == revision &&
            result.StructureVerified &&
            result.ImageDigestVerified &&
            result.SignaturePresent &&
            string.Equals(
                context.ProductKey,
                selectedProduct.EngineeringKey,
                StringComparison.Ordinal) &&
            string.Equals(
                result.ProductKey,
                selectedProduct.EngineeringKey,
                StringComparison.Ordinal) &&
            result.ProductMatched &&
            !result.AdmissionAllowed;
    }

    private LoaderSessionSnapshot CreateSnapshot()
    {
        if (selectedProduct is null)
        {
            return new LoaderSessionSnapshot(
                revision,
                Product: null,
                Status: "Choose the Limited Underground system you are working with.");
        }

        return new LoaderSessionSnapshot(
            revision,
            selectedProduct,
            $"{selectedProduct.DisplayName} selected. Inspection provider unavailable; firmware installation remains disabled.");
    }
}
