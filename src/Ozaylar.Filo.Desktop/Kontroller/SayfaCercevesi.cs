using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Ozaylar.Filo.Desktop.Kontroller;

/// <summary>
/// Sayfa çerçevesi: sayfayı DI'dan alır (menü sayfaları tekil, detay sayfası geçici), isteğe bağlı modeli DataContext yapar,
/// yumuşak "kayarak belirme" geçişiyle gösterir. Gezinme geçmişini Gezgin tutar (Frame günlüğü kullanılmaz).
/// </summary>
public sealed class SayfaCercevesi : Frame
{
    public event Action<FrameworkElement>? Gecildi;

    public SayfaCercevesi()
    {
        NavigationUIVisibility = NavigationUIVisibility.Hidden;
        Focusable = false;
        Navigated += (_, _) =>
        {
            while (CanGoBack) RemoveBackEntry(); // geçmiş Gezgin'de; sayfalar bellekte birikmesin
            if (Content is FrameworkElement sayfa)
            {
                Gec(sayfa);
                Gecildi?.Invoke(sayfa);
            }
        };
    }

    public void Git(Type sayfaTipi, object? model = null)
    {
        var sayfa = (FrameworkElement)(App.Servisler.GetService(sayfaTipi) ?? ActivatorUtilities.CreateInstance(App.Servisler, sayfaTipi));
        if (model is not null) sayfa.DataContext = model;
        if (ReferenceEquals(Content, sayfa))
        {
            Gec(sayfa);
            Gecildi?.Invoke(sayfa);
            return;
        }
        Navigate(sayfa);
    }

    private static void Gec(FrameworkElement sayfa)
    {
        var sure = TimeSpan.FromMilliseconds(240);
        var kolay = new CubicEase { EasingMode = EasingMode.EaseOut };
        var kayma = new TranslateTransform(0, 14);
        sayfa.RenderTransform = kayma;
        kayma.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, sure) { EasingFunction = kolay });
        sayfa.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, sure) { EasingFunction = kolay });
    }
}
