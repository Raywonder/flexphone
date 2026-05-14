using System.Windows;
using System.Windows.Input;

namespace FlexPhone.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow(bool gettingStarted)
        {
            InitializeComponent();
            HelpTabs.SelectedIndex = gettingStarted ? 0 : 1;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
