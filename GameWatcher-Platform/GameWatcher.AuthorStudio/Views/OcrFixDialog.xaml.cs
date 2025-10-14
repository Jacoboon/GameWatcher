using System.Windows;
using GameWatcher.AuthorStudio.ViewModels;

namespace GameWatcher.AuthorStudio.Views
{
    /// <summary>
    /// Dialog for creating OCR fix rules with user confirmation.
    /// </summary>
    public partial class OcrFixDialog : Window
    {
        public OcrFixDialogViewModel ViewModel { get; }
        public bool WasCreated { get; private set; }

        public OcrFixDialog(OcrFixDialogViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        private void CreateAndApply_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsValid)
            {
                MessageBox.Show(
                    ViewModel.ValidationMessage,
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            WasCreated = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            WasCreated = false;
            DialogResult = false;
            Close();
        }
    }
}
