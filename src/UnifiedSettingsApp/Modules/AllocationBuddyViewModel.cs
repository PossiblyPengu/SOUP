                public ObservableCollection<ItemAllocation> ItemPool { get; set; } = new();

                public RelayCommand<ItemAllocation> MoveItemToPoolCommand { get; }
                public RelayCommand<ItemAllocation> MoveItemFromPoolCommand { get; }

                public AllocationBuddyViewModel()
                {
                    LoadDictionaryData();
                    MoveItemToPoolCommand = new RelayCommand<ItemAllocation>(MoveItemToPool);
                    MoveItemFromPoolCommand = new RelayCommand<ItemAllocation>(MoveItemFromPool);
                }

                private void MoveItemToPool(ItemAllocation item)
                {
                    // Remove from location and add to pool
                    var loc = LocationAllocations.FirstOrDefault(l => l.Items.Contains(item));
                    if (loc != null)
                    {
                        loc.Items.Remove(item);
                        ItemPool.Add(item);
                        // Notify UI of changes
                        OnPropertyChanged(nameof(LocationAllocations));
                        OnPropertyChanged(nameof(ItemPool));
                        // Also notify for each location's Items collection
                        foreach (var l in LocationAllocations)
                            OnPropertyChanged(nameof(l.Items));
                    }
                }

                private void MoveItemFromPool(ItemAllocation item)
                {
                    // Move from pool to first location (or prompt for location)
                    if (ItemPool.Contains(item) && LocationAllocations.Count > 0)
                    {
                        ItemPool.Remove(item);
                        LocationAllocations[0].Items.Add(item);
                        OnPropertyChanged(nameof(LocationAllocations));
                        OnPropertyChanged(nameof(ItemPool));
                        foreach (var l in LocationAllocations)
                            OnPropertyChanged(nameof(l.Items));
                    }
                }
        public ObservableCollection<LocationAllocation> LocationAllocations { get; set; } = new();

        public void UpdateAllocations(IEnumerable<(AllocationRecord Allocation, DictionaryItem DictionaryItem)> matched)
        {
            var grouped = matched
                .GroupBy(x => x.Allocation.Location)
                .OrderBy(g => g.Key)
                .Select(g => new LocationAllocation
                {
                    Location = g.Key,
                    Items = g.Select(x => new ItemAllocation
                    {
                        ItemNumber = x.Allocation.ItemNumber,
                        Description = x.DictionaryItem?.Description ?? "Unknown",
                        Quantity = x.Allocation.Quantity
                    }).ToList()
                });
            LocationAllocations.Clear();
            foreach (var loc in grouped)
                LocationAllocations.Add(loc);
        }

        public class LocationAllocation
        {
            public string Location { get; set; } = "";
            public List<ItemAllocation> Items { get; set; } = new();
        }

        public class ItemAllocation
        {
            public string ItemNumber { get; set; } = "";
            public string Description { get; set; } = "";
            public int Quantity { get; set; }
        }
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnifiedSettingsApp.Modules
    public List<DictionaryItem> DictionaryItems { get; set; } = new();
            LoadDictionaryData();
{
    public class AllocationBuddyViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<LocationAllocation> Locations { get; } = new();
        public ObservableCollection<ItemAllocation> ItemPool { get; } = new();
        public string ImportedFileName { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

        private void LoadDictionaryData()
        {
            // Load and parse dictionaries.js
            var dictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "UnifiedApp", "src", "renderer", "modules", "allocation-buddy", "src", "js", "dictionaries.js");
            if (!File.Exists(dictPath)) return;
            var js = File.ReadAllText(dictPath);
            // Extract items array using regex
            var match = Regex.Match(js, "items:\s*\[(.*?)\],", RegexOptions.Singleline);
            if (!match.Success) return;
            var itemsBlock = match.Groups[1].Value;
            var itemRegex = new Regex(@"\{\s*number: '([^']+)',\s*description: '([^']+)',\s*skus: \[(.*?)\]\s*\}", RegexOptions.Singleline);
            foreach (Match m in itemRegex.Matches(itemsBlock))
            {
                var number = m.Groups[1].Value;
                var desc = m.Groups[2].Value;
                var skusRaw = m.Groups[3].Value;
                var skus = Regex.Matches(skusRaw, "'([^']+)'")
                    .Cast<Match>()
                    .Select(x => x.Groups[1].Value)
                    .ToList();
                DictionaryItems.Add(new DictionaryItem { Number = number, Description = desc, Skus = skus });
            }
        }

    public class LocationAllocation
    {
        public string Location { get; set; } = "";
        public ObservableCollection<ItemAllocation> Items { get; } = new();
    }

    public class ItemAllocation
    {
        public string ItemNo { get; set; } = "";
        public string Description { get; set; } = "";
        public int Quantity { get; set; }
    }
}
