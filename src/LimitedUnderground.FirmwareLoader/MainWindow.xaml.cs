using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LimitedUnderground.FirmwareLoader;

public partial class MainWindow : Window
{
    private readonly LoaderSessionController session = new(
        LoaderProductionProviders.Factories,
        LoaderProductionProviders.SignerTrustPolicies);

    public MainWindow()
    {
        InitializeComponent();
        Title = LoaderProductCatalog.WindowTitle;
        PublishSession();
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        ClearInspection();
        session.Dispose();
        base.OnClosed(eventArgs);
    }

    private void ProductChoiceButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is not Button { Tag: string engineeringKey } ||
            !session.SelectProduct(engineeringKey))
        {
            return;
        }

        ClearInspection();
        PublishSession();
    }

    private void BackButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        ClearInspection();
        session.ReturnToProductChoice();
        PublishSession();
    }

    private void ChooseBundleButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        var dialog = new OpenFileDialog
        {
            Title = "Choose a firmware candidate to inspect",
            Filter = "Firmware candidate archives (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
            AddToRecent = false,
            Multiselect = false,
            ValidateNames = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ClearInspection();
        OfflineBundleInspectionOutcome outcome;
        try
        {
            using var candidate = new FileStream(
                dialog.FileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            outcome = OfflineBundleInspectionWorkflow.Inspect(session, candidate);
        }
        catch (IOException)
        {
            outcome = FileUnavailable();
        }
        catch (UnauthorizedAccessException)
        {
            outcome = FileUnavailable();
        }

        PublishInspection(outcome);
    }

    private void PublishInspection(OfflineBundleInspectionOutcome outcome)
    {
        InspectionHeading.Text = outcome.Heading;
        InspectionSummary.Text = outcome.Summary;
        InspectionProduct.Text = $"Product: {outcome.ProductKey}";
        InspectionTarget.Text = $"Target: {outcome.TargetKey}";
        InspectionImageBytes.Text = $"Image bytes: {outcome.ImageBytes}";
        InspectionStructure.Text = $"Structure: {outcome.StructureStatus}";
        InspectionDigest.Text = $"Image SHA-256: {outcome.ImageDigestStatus}";
        InspectionSignature.Text = $"Signature: {outcome.SignatureStatus}";
        InspectionTrust.Text = $"Signer trust: {outcome.SignerTrustStatus}";
        InspectionAdmission.Text = $"Installation admission: {outcome.AdmissionStatus}";
        InspectionResultPanel.Visibility = Visibility.Visible;
        InspectionHeading.Focus();
    }

    private void ClearInspection()
    {
        InspectionResultPanel.Visibility = Visibility.Collapsed;
        InspectionHeading.Text = string.Empty;
        InspectionSummary.Text = string.Empty;
        InspectionProduct.Text = string.Empty;
        InspectionTarget.Text = string.Empty;
        InspectionImageBytes.Text = string.Empty;
        InspectionStructure.Text = string.Empty;
        InspectionDigest.Text = string.Empty;
        InspectionSignature.Text = string.Empty;
        InspectionTrust.Text = string.Empty;
        InspectionAdmission.Text = string.Empty;
    }

    private static OfflineBundleInspectionOutcome FileUnavailable() =>
        new(
            false,
            "File unavailable",
            "The selected file could not be opened for read-only inspection.",
            "Not published",
            "Not published",
            "Not published",
            "Not accepted",
            "Not accepted",
            "Not accepted",
            "Not configured",
            "Blocked");


    private void PublishSession()
    {
        var snapshot = session.Snapshot;
        ProductChoicePanel.Visibility = snapshot.HasProduct
            ? Visibility.Collapsed
            : Visibility.Visible;
        SelectedProductPanel.Visibility = snapshot.HasProduct
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (snapshot.Product is null)
        {
            SelectedProductName.Text = string.Empty;
            SelectedProductDescription.Text = string.Empty;
            SelectedProductStatus.Text = string.Empty;
            ContinueButton.IsEnabled = false;
            return;
        }

        SelectedProductName.Text = snapshot.Product.DisplayName;
        SelectedProductDescription.Text = snapshot.Product.Description;
        SelectedProductStatus.Text = snapshot.Status;
        ContinueButton.IsEnabled = snapshot.OfflineBundleInspectionAvailable;
    }
}
