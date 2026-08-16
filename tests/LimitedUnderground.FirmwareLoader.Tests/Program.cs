using LimitedUnderground.FirmwareLoader;

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
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

Console.WriteLine($"{tests.Length} shared loader foundation groups passed.");

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

static void Require(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException("FAILED: " + name);
    }
}
