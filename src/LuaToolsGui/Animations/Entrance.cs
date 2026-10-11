using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LuaToolsGui.Animations;

/// <summary>
/// One-shot, subtle page-entrance animation: the element fades in while sliding up a few
/// pixels. Fires on the first Loaded only, so cached navigation pages stay put and there is
/// no re-animation jank when switching back to an already-opened page.
/// </summary>
public static class Entrance
{
    public static void Enable(FrameworkElement element)
    {
        element.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var fe = (FrameworkElement)sender;
        fe.Loaded -= OnLoaded;

        var slide = new TranslateTransform(0, 14);
        fe.RenderTransform = slide;
        fe.RenderTransformOrigin = new Point(0.5, 0.5);
        fe.Opacity = 0;

        var sb = new Storyboard();
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        Storyboard.SetTarget(fade, fe);
        Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));

        var up = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(up, slide);
        Storyboard.SetTargetProperty(up, new PropertyPath(TranslateTransform.YProperty));

        sb.Children.Add(fade);
        sb.Children.Add(up);
        sb.Begin(fe);
    }
}