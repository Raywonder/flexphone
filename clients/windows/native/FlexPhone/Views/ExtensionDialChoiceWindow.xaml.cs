using System.Windows;
using System.Windows.Input;

namespace FlexPhone.Views
{
    internal partial class ExtensionDialChoiceWindow : Window
    {
        internal ExtensionDialChoiceWindow(string extension, IEnumerable<MainWindow.ExtensionDialChoice> choices)
        {
            InitializeComponent();
            InstructionText.Text = $"Extension {extension} exists on more than one phone system. Choose which user to dial.";
            ChoicesList.ItemsSource = choices.ToList();
            Loaded += (_, _) =>
            {
                if (ChoicesList.Items.Count > 0)
                {
                    ChoicesList.SelectedIndex = 0;
                    ChoicesList.Focus();
                }
            };
        }

        internal MainWindow.ExtensionDialChoice? SelectedChoice => ChoicesList.SelectedItem as MainWindow.ExtensionDialChoice;

        private void DialButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedChoice is null)
            {
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ChoicesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DialButton_Click(sender, e);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
            }
        }
    }
}
