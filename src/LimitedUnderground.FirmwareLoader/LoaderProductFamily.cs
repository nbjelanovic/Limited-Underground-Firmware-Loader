namespace LimitedUnderground.FirmwareLoader;

public sealed record LoaderProductFamily(
    string EngineeringKey,
    string DisplayName,
    string Description,
    string ProviderStatus)
{
    public bool ConnectedDeviceInspectionAvailable => false;

    public bool FirmwareBundleSelectionAvailable => false;

    public bool DeviceBundleMatchAvailable => false;

    public bool FirmwareWritingAvailable => false;

    public bool RecoveryAvailable => false;
}

public static class LoaderProductCatalog
{
    public const string ParentName = "Limited Underground";
    public const string ProductName = "Firmware Loader";
    public const string ReleaseStage = "Preview";

    public static LoaderProductFamily Trail { get; } = new(
        EngineeringKey: "opentrail",
        DisplayName: "Limited Underground Trail",
        Description: "Trail communication devices and companion-system firmware.",
        ProviderStatus: "The existing Trail inspection provider has not yet been migrated into this shared application.");

    public static LoaderProductFamily Display { get; } = new(
        EngineeringKey: "opengauge",
        DisplayName: "Limited Underground Display",
        Description: "Vehicle display, gauge, and gateway firmware.",
        ProviderStatus: "A Display loader provider and accepted target manifest do not exist yet.");

    public static IReadOnlyList<LoaderProductFamily> All { get; } =
        Array.AsReadOnly([Trail, Display]);

    public static LoaderProductFamily? Find(string engineeringKey)
    {
        ArgumentNullException.ThrowIfNull(engineeringKey);
        return All.FirstOrDefault(product =>
            string.Equals(
                product.EngineeringKey,
                engineeringKey,
                StringComparison.Ordinal));
    }

    public static string WindowTitle =>
        $"{ParentName} {ProductName} — {ReleaseStage}";
}
