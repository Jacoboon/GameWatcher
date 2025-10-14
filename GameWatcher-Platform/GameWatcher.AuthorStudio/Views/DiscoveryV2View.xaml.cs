using System.Windows.Controls;
using GameWatcher.AuthorStudio.ViewModels;

namespace GameWatcher.AuthorStudio.Views
{
    /// <summary>
    /// Discovery V2 - Enhanced dialogue discovery with List + Details pane design.
    /// </summary>
    public partial class DiscoveryV2View : UserControl
    {
        public DiscoveryV2View()
        {
            InitializeComponent();
        }

        private void DialogueText_Changed(object sender, TextChangedEventArgs e)
        {
            // Notify ViewModel that text changed so it can re-check for OCR fixes
            if (DataContext is DiscoveryV2ViewModel viewModel)
            {
                viewModel.OnDialogueTextChanged();
            }
        }
    }
}
