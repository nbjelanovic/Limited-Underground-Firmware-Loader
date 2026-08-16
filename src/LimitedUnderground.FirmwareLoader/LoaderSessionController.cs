namespace LimitedUnderground.FirmwareLoader;

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
