using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PrismCodingAgent.CodingShell.Views;

/// <summary>以等比例居中方式显示二进制图片；无效数据不显示图片。</summary>
public sealed class BinaryImage : Image
{
    /// <summary>图片数据属性。</summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(BinaryData), typeof(BinaryImage), new PropertyMetadata(null, OnDataChanged));

    /// <summary>初始化等比例图片控件。</summary>
    public BinaryImage() => Stretch = Stretch.Uniform;

    /// <summary>获取或设置图片数据。</summary>
    public BinaryData? Data
    {
        get => (BinaryData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private static void OnDataChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var image = (BinaryImage)sender;
        image.Source = null;
        if (args.NewValue is not BinaryData data)
        {
            return;
        }

        try
        {
            using Stream stream = data.ToStream();
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or IOException)
        {
            // Corrupt attachments remain blank, matching the Avalonia image control.
            image.Source = null;
        }
    }
}
