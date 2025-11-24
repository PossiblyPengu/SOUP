using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using CsvHelper;
using System.Globalization;
using System.Windows.Controls;
using Microsoft.Win32;
using UnifiedSettingsApp.Modules;

namespace UnifiedSettingsApp.Modules
{
    public partial class AllocationBuddyControl : UserControl
    {
        private AllocationBuddyViewModel _vm = new AllocationBuddyViewModel();
        public AllocationBuddyControl()
        {
            InitializeComponent();
            DataContext = _vm;
            ImportFileBtn.Click += ImportFileBtn_Click;
        }

        private void ImportFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Excel/CSV Files|*.xlsx;*.xls;*.csv|All Files|*.*";
                var dlg = new OpenFileDialog
                {
                    Filter = "Excel/CSV Files (*.csv;*.xlsx)|*.csv;*.xlsx|All Files (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    var filePath = dlg.FileName;
                    List<AllocationRecord> allocations = new();
                    if (filePath.EndsWith(".csv"))
                    {
                        using var reader = new StreamReader(filePath);
                        using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
                        allocations = csv.GetRecords<AllocationRecord>().ToList();
                    }
                    // TODO: Add Excel parsing if needed
                    // Match allocations to dictionary
                    var vm = DataContext as AllocationBuddyViewModel;
                    if (vm == null) return;
                    var matched = allocations.Select(a => new
                    {
                        Allocation = a,
                        DictionaryItem = vm.DictionaryItems.FirstOrDefault(d => d.Number == a.ItemNumber)
                    }).ToList();
                    vm.UpdateAllocations(matched);
                }

                public class AllocationRecord
                {
                    public string ItemNumber { get; set; } = "";
                    public int Quantity { get; set; }
                    public string Location { get; set; } = "";
                }
            if (dlg.ShowDialog() == true)
            {
                _vm.ImportedFileName = dlg.FileName;
                // TODO: Parse file, match allocations, update Locations and ItemPool
                // For now, just show file name
                ImportedFileName.Text = dlg.FileName;
            }
        }
    }
}
