using System.Windows;
using Wpf.Ui.Controls;
using WallP.Models;

namespace WallP.Views;

public partial class AddCollectionDialog : FluentWindow
{
    public string Username => UsernameBox.Text.Trim();
    public int CollectionId { get; private set; }
    public string DisplayName => DisplayNameBox.Text.Trim();

    public AddCollectionDialog(AppSettings settings)
    {
        InitializeComponent();
        UsernameBox.Text = settings.WallhavenUsername;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ShowError("Username is required.");
            return;
        }

        if (!int.TryParse(CollectionIdBox.Text.Trim(), out var id) || id <= 0)
        {
            ShowError("Collection ID must be a positive number.");
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ShowError("Display name is required.");
            return;
        }

        CollectionId = id;
        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.Visibility = Visibility.Visible;
    }
}
