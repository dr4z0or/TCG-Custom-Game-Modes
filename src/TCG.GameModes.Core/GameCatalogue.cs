using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

using static TCG.Shared.Lang;
namespace TCG.GameModes.Core
{
    // Products and furniture the game offered during the last session, so that the rules
    // screen (at the title menu, before the game data is loaded) can list them by name.
    public sealed class CatalogueEntry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
    }

    public sealed class GameCatalogue
    {
        public const string FileName = "game-catalogue.xml";
        public List<CatalogueEntry> Items { get; set; } = new List<CatalogueEntry>();
        public List<CatalogueEntry> Furniture { get; set; } = new List<CatalogueEntry>();

        public bool IsEmpty => Items.Count == 0 && Furniture.Count == 0;

        static string PathFor(string campaigns)
        {
            if (!System.IO.Path.IsPathRooted(campaigns) || !File.Exists(System.IO.Path.Combine(campaigns, ".tcg-save-lab"))) throw new IOException(T("Espace de boutiques invalide."));
            return System.IO.Path.Combine(campaigns, FileName);
        }

        public static GameCatalogue Load(string campaigns)
        {
            try
            {
                var path = PathFor(campaigns);
                if (!File.Exists(path)) return new GameCatalogue();
                using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                return (GameCatalogue?)new XmlSerializer(typeof(GameCatalogue)).Deserialize(reader) ?? new GameCatalogue();
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is XmlException) { return new GameCatalogue(); }
        }

        // Merges what the game shows now into the stored catalogue (items of mods not loaded
        // this time are kept) and writes it only when something changed.
        public static GameCatalogue Merge(string campaigns, IEnumerable<CatalogueEntry> items, IEnumerable<CatalogueEntry> furniture)
        {
            var stored = Load(campaigns);
            var changed = MergeInto(stored.Items, items) | MergeInto(stored.Furniture, furniture);
            if (changed)
            {
                var path = PathFor(campaigns);
                var temporary = path + ".pending";
                using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                    new XmlSerializer(typeof(GameCatalogue)).Serialize(writer, stored);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            return stored;
        }

        static bool MergeInto(List<CatalogueEntry> stored, IEnumerable<CatalogueEntry> current)
        {
            var changed = false;
            foreach (var entry in current.Where(e => !string.IsNullOrWhiteSpace(e.Id)))
            {
                var existing = stored.FirstOrDefault(e => e.Id == entry.Id);
                if (existing == null) { stored.Add(new CatalogueEntry { Id = entry.Id, Name = entry.Name, Level = entry.Level }); changed = true; }
                else if (existing.Name != entry.Name || existing.Level != entry.Level) { existing.Name = entry.Name; existing.Level = entry.Level; changed = true; }
            }
            return changed;
        }
    }
}
