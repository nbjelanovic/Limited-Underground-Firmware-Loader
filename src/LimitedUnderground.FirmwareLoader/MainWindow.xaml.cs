using System.Windows;
using System.Windows.Controls;

namespace LimitedUnderground.FirmwareLoader;

public partial class MainWindow : Window
{
    private readonly LoaderSessionController session = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = LoaderProductCatalog.WindowTitle;
        PublishSession();
    }

    private void ProductChoiceButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is not Button { Tag: string engineeringKey } ||
            !session.SelectProduct(engineeringKey))
        {
            return;
        }

        PublishSession();
    }

    private void BackButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        session.ReturnToProductChoice();
        PublishSession();
    }

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
        SelectedProductStatus.Text = snapshot.Product.ProviderStatus +
            " Firmware installation remains disabled.";
        ContinueButton.IsEnabled = snapshot.ConnectedDeviceInspectionAvailable;
    }
}
