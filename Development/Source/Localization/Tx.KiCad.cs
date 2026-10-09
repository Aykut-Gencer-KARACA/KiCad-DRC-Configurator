using System;
using System.Collections.Generic;
using System.Linq;

namespace KiCadDrc
{
    static partial class Tx
    {
        // ---------------------------------------------------------------- KiCad location
        public static string KiCadNotFound { get { return L("No KiCad installation was found.", "KiCad kurulumu bulunamadı."); } }
        public static string KiCadNotFoundIntro
        {
            get
            {
                return L("KiCad could not be found automatically on this computer.\n\nIn the next window select the folder KiCad is installed in " +
                         "(the folder containing bin\\kicad.exe, e.g. C:\\Program Files\\KiCad\\10.0).",
                         "KiCad bu bilgisayarda otomatik bulunamadı.\n\nSıradaki pencerede KiCad'in kurulu olduğu klasörü seç " +
                         "(içinde bin\\kicad.exe olan klasör, örn. C:\\Program Files\\KiCad\\10.0).");
            }
        }
        public static string KiCadFolderTitle { get { return L("KiCad folder", "KiCad klasörü"); } }
        public static string KiCadFolderPrompt { get { return L("Select the KiCad installation folder (the one containing bin\\kicad.exe)", "KiCad kurulum klasörünü seç (içinde bin\\kicad.exe olan)"); } }
        public static string KiCadNotInFolder(string folder)
        {
            return L("KiCad was not found in this folder:\n" + folder + "\n\nSelect the KiCad installation folder that contains bin\\kicad.exe.",
                     "Bu klasörde KiCad bulunamadı:\n" + folder + "\n\nİçinde bin\\kicad.exe olan KiCad kurulum klasörünü seç.");
        }
        public static string DataFolderNotReady(string message)
        {
            return L("The \"" + DiskNames.DataFolder + "\" folder could not be prepared: " + message + "\nRun the app from a folder outside Desktop/Documents/Program Files (e.g. C:\\Projects\\DRC).",
                     "\"" + DiskNames.DataFolder + "\" klasörü hazırlanamadı: " + message + "\nUygulamayı Masaüstü/Belgeler/Program Files dışında bir klasörde (örn. C:\\Projects\\DRC) çalıştır.");
        }
        public static string DataFolderNotWritable
        {
            get
            {
                return L("The \"" + DiskNames.DataFolder + "\" folder could not be written. Run the app from a folder outside Desktop/Documents/Program Files (e.g. C:\\Projects\\DRC).",
                         "\"" + DiskNames.DataFolder + "\" klasörüne yazılamadı. Uygulamayı Masaüstü/Belgeler/Program Files dışında bir klasörde (örn. C:\\Projects\\DRC) çalıştır.");
            }
        }
        public static string WarnStateCorrupt
        {
            get
            {
                return L("The record file (state.json) is corrupt and was ignored. It is recreated when you press Apply or Restore defaults.",
                         "Kayıt dosyası (state.json) bozuk olduğu için yok sayıldı. Uygula'ya ya da Varsayılana dön'e basınca yeniden oluşur.");
            }
        }
        public static string WarnTemplateDeleted(string name)
        {
            return L("The applied template (" + name + ") was deleted. Press Apply to recreate it with the same settings.",
                     "Uygulanan şablon (" + name + ") silinmiş. Aynı ayarlarla yeniden oluşturmak için Uygula'ya bas.");
        }
        public static string WarnStockChangedByOld
        {
            get
            {
                return L("KiCad's plain \"New Project\" file was changed by an older version of this app. It is no longer touched; " +
                         "pressing Apply or Restore defaults returns it to KiCad's original.",
                         "KiCad'in düz \"New Project\" dosyası uygulamanın eski bir sürümünce değiştirilmiş. Artık ona dokunulmuyor; " +
                         "Uygula'ya ya da Varsayılana dön'e basınca KiCad'in orijinaline döner.");
            }
        }
        public static string WarnStockChangedNoRecord
        {
            get
            {
                return L("KiCad's new project template was changed before but no record was found (the data folder may have been deleted). " +
                         "Pick a setting and press Apply or Restore defaults; both work fine.",
                         "KiCad'in yeni proje şablonu daha önce değiştirilmiş ama kaydı bulunamadı (" + DiskNames.DataFolder + " silinmiş olabilir). " +
                         "Bir ayar seçip Uygula'ya ya da Varsayılana dön'e bas; ikisi de sorunsuz çalışır.");
            }
        }
        public static string WarnStaleTemplatePath
        {
            get
            {
                return L("KiCad's template folder setting points to a folder that is no longer used. Restore defaults clears it.",
                         "KiCad'in şablon klasörü ayarı artık kullanılmayan bir klasörü gösteriyor. Varsayılana dön bunu temizler.");
            }
        }
        public static string AdminDenied(string installDir)
        {
            return L("Administrator permission was not given; nothing was written to KiCad.\n\nKiCad is installed in " + installDir +
                     " and writing there needs administrator rights. Try again and press \"Yes\" in the Windows prompt.",
                     "Yönetici izni verilmedi, KiCad'e hiçbir şey yazılmadı.\n\nKiCad " + installDir + " klasörüne kurulu ve oraya yazmak yönetici izni istiyor. " +
                     "Tekrar deneyip açılan Windows penceresinde \"Evet\"e bas.");
        }
        public static string AdminCopyFailed(int code, string path)
        {
            return L("KiCad's file could not be written even with administrator rights (code " + code + "):\n" + path,
                     "KiCad'in dosyası yönetici izniyle de yazılamadı (kod " + code + "):\n" + path);
        }
        public static string NeedsKiCad10(string version)
        {
            return L("This app generates the KiCad 10 file format; the KiCad found is version " + version + ".",
                     "Bu uygulama KiCad 10 dosya formatı üretir; bulunan sürüm KiCad " + version + ".");
        }
        public static string KiCadCliNotFound(string path) { return L("kicad-cli not found: ", "kicad-cli bulunamadı: ") + path; }
        public static string KiCadCliNotFoundVerify(string path) { return L("kicad-cli not found, verification could not run:\n", "kicad-cli bulunamadı, doğrulama yapılamadı:\n") + path; }
        public static string KiCadCliTimeout { get { return L("kicad-cli timed out.", "kicad-cli zaman aşımına uğradı."); } }
        public static string KiCadCliFailed(int code, string output) { return L("kicad-cli failed (", "kicad-cli hata verdi (") + code + "):\n" + output; }
        public static string KiCadCouldNotOpenGenerated(string output) { return L("KiCad could not open the generated files:\n", "KiCad üretilen dosyaları açamadı:\n") + output; }
        public static string KiCadCouldNotOpenProject(string output) { return L("KiCad could not open the project: ", "KiCad projeyi açamadı: ") + output; }
    }
}
