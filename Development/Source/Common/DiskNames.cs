using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    // Names of folders and files on disk. They do not change with the language.
    static class DiskNames
    {
        public const string DataFolder = "Data";                      // next to the exe
        public const string BackupFolder = "Backup";                  // inside the data folder
        public const string TemplatesFolder = "Templates";            // inside the data folder
        public const string ProjectBackups = "Project Backups";       // inside the data folder
        public const string ProductionFolder = "JLC_Production";      // inside the KiCad project folder
        // Names used by versions before 2.2; converted, moved or removed at startup
        public const string LegacyDataFolder = "Uygulama Dosyaları";
        public const string LegacyProjectBackups = "proje yedekleri";
        public const string LegacySettingsFile = "ayarlar.json";
        public const string LegacyStateFile = "durum.json";
        public const string LegacyPartCacheFile = "parca-onbellek.json";
    }
}
