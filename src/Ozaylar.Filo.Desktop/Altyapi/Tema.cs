using System.IO;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Ozaylar.Filo.Desktop.Altyapi;

/// <summary>
/// Uygulama teması: turuncu vurgu + açık/koyu palet (Temalar/Acik.xaml, Koyu.xaml). Seçim kullanıcı başına
/// %LocalAppData%\OzaylarFilo\arayuz.json dosyasında hatırlanır.
/// </summary>
public static class Tema
{
    private static readonly Color Turuncu = Color.FromRgb(0xF9, 0x73, 0x16);
    private static string Dosya => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OzaylarFilo", "arayuz.json");

    public static bool Koyu { get; private set; }
    public static event Action? Degisti;

    public static void Yukle()
    {
        try { Koyu = File.Exists(Dosya) && File.ReadAllText(Dosya).Contains("\"koyu\":true"); }
        catch (IOException) { }
        Uygula(Koyu, kaydet: false);
    }

    public static void Degistir() => Uygula(!Koyu);

    public static void Uygula(bool koyu, bool kaydet = true)
    {
        Koyu = koyu;
        var tema = koyu ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(tema, WindowBackdropType.None, updateAccent: false);
        // Açık temada dolgu (Primary düğme) koyu turuncu: beyaz yazı okunaklı kalır.
        ApplicationAccentColorManager.Apply(Turuncu,
            koyu ? Color.FromRgb(0xFB, 0x92, 0x3C) : Color.FromRgb(0xEA, 0x58, 0x0C),
            koyu ? Color.FromRgb(0xFD, 0xBA, 0x74) : Color.FromRgb(0xC2, 0x41, 0x0C),
            koyu ? Color.FromRgb(0xFE, 0xD7, 0xAA) : Color.FromRgb(0x9A, 0x34, 0x12));

        var sozluk = System.Windows.Application.Current.Resources.MergedDictionaries;
        var eski = sozluk.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Temalar/") == true);
        var yeni = new ResourceDictionary { Source = new Uri($"pack://application:,,,/OzaylarFilo;component/Temalar/{(koyu ? "Koyu" : "Acik")}.xaml") };
        if (eski is null) sozluk.Add(yeni);
        else sozluk[sozluk.IndexOf(eski)] = yeni;

        if (kaydet)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Dosya)!);
                File.WriteAllText(Dosya, $"{{\"koyu\":{(koyu ? "true" : "false")}}}");
            }
            catch (IOException) { }
        }
        Degisti?.Invoke();
    }
}
