using System;
using System.IO;
using Newtonsoft.Json;
namespace TCG.GameModes
{
    public sealed class PackFile
    {
        public string BundleId="",Name="";
        public PackExpansion[] CardExpansions=Array.Empty<PackExpansion>();
        public PackItem[] Items=Array.Empty<PackItem>();
    }
    public sealed class PackExpansion { public string CardExpansion="",MenuCategory="",Name=""; }
    public sealed class PackItem
    {
        public string ItemType="",CardExpansion="",SpawnsPackType="",PhoneAppId="";
        public bool IsCardPack,IsCardBox,IsBulkBox,IsSprayCan,AddItemAsFigurine,AddItemAsBoardGame;
    }
    public static class PackDefinitions
    {
        public static PackFile Read(string path)
        {
            using(var stream=File.OpenText(path))
            using(var reader=new JsonTextReader(stream){MaxDepth=128})
            {
                var serializer=JsonSerializer.Create(new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MetadataPropertyHandling=MetadataPropertyHandling.Ignore});
                var value=serializer.Deserialize<PackFile>(reader)??throw new IOException("Définition vide.");
                value.Items=value.Items??Array.Empty<PackItem>();value.CardExpansions=value.CardExpansions??Array.Empty<PackExpansion>();
                return value;
            }
        }
    }
}
