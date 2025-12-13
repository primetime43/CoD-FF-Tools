using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Call_of_Duty_FastFile_Editor.Models
{
    // not sure About this, revisit this. This is a global holder of the asset records to be accessible anywhere.
    public class AssetRecordCollection
    {
        public List<RawFileNode> RawFileNodes { get; set; } = new List<RawFileNode>();
        public List<StringTable> StringTables { get; set; } = new List<StringTable>();
        public List<LocalizedEntry> LocalizedEntries { get; set; } = new List<LocalizedEntry>();
        public List<MenuList> MenuLists { get; set; } = new List<MenuList>();
        public List<MaterialAsset> Materials { get; set; } = new List<MaterialAsset>();
        public List<TechSetAsset> TechSets { get; set; } = new List<TechSetAsset>();
        public List<XAnimParts> XAnims { get; set; } = new List<XAnimParts>();
        public List<WeaponAsset> Weapons { get; set; } = new List<WeaponAsset>();
        public List<ImageAsset> Images { get; set; } = new List<ImageAsset>();
        public List<ZoneAssetRecord> UpdatedRecords { get; set; } = new List<ZoneAssetRecord>();
    }

}
