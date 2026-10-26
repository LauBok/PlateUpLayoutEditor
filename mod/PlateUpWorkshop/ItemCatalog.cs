using System.Collections.Generic;
using System.Text.RegularExpressions;
using KitchenData;

namespace KitchenLayoutEditor
{
    /// <summary>Items a smart grabber can be told to take, for the filter picker.</summary>
    internal static class ItemCatalog
    {
        internal sealed class Entry
        {
            public int Id;
            public string Name = "";
        }

        private static List<Entry>? _entries;
        private static Dictionary<int, Entry>? _byId;

        public static IReadOnlyList<Entry> Entries
        {
            get { EnsureLoaded(); return _entries ?? new List<Entry>(); }
        }

        public static string DisplayName(int id)
        {
            EnsureLoaded();
            return _byId != null && _byId.TryGetValue(id, out var e) ? e.Name : "item #" + id;
        }

        private static void EnsureLoaded()
        {
            if (_entries != null || GameData.Main == null) return;
            var list = new List<Entry>();
            foreach (var item in GameData.Main.Get<Item>())
            {
                if (item == null) continue;
                // Asset names such as "CoffeeCupCoffee" become "Coffee Cup Coffee".
                list.Add(new Entry { Id = item.ID, Name = Regex.Replace(item.name ?? "", "(?<=[a-z0-9])(?=[A-Z])", " ") });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var byId = new Dictionary<int, Entry>();
            foreach (var e in list) byId[e.Id] = e;
            _entries = list;
            _byId = byId;
        }
    }
}
