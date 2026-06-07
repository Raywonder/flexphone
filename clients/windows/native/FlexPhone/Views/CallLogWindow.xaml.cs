using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using FlexPhone.Models;

namespace FlexPhone.Views
{
    public partial class CallLogWindow : Window
    {
        private readonly ObservableCollection<CallLogEntry> _source;
        private readonly ObservableCollection<CallLogEntry> _filtered = [];

        public CallLogWindow(ObservableCollection<CallLogEntry> source)
        {
            InitializeComponent();
            _source = source;
            EntriesListBox.ItemsSource = _filtered;
            FilterComboBox.SelectedIndex = 0;
            _source.CollectionChanged += Source_CollectionChanged;
            RefreshFilter();
            Closed += (_, _) => _source.CollectionChanged -= Source_CollectionChanged;
        }

        private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshFilter();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshFilter();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshFilter()
        {
            if (!IsInitialized)
            {
                return;
            }

            var selected = (FilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            _filtered.Clear();
            foreach (var entry in _source.Where(entry =>
                         selected.Equals("All", StringComparison.OrdinalIgnoreCase)
                         || entry.Category.Equals(selected.TrimEnd('s'), StringComparison.OrdinalIgnoreCase)
                         || entry.Category.Equals(selected, StringComparison.OrdinalIgnoreCase)))
            {
                _filtered.Add(entry);
            }
        }
    }
}
