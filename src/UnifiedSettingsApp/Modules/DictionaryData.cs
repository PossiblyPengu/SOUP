using System.Collections.Generic;

namespace UnifiedSettingsApp.Modules
{
    public class DictionaryItem
    {
        public string Number { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Skus { get; set; } = new();
    }

    public class DictionaryData
    {
        public List<DictionaryItem> Items { get; set; } = new();
    }
}
