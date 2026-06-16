using System.Windows;
using System.Windows.Media.Imaging;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class ImageAdjustmentDialog : Window
{
    public int Brightness { get; private set; }
    public int Contrast { get; private set; }

    private readonly BitmapSource _baseImage;

    public ImageAdjustmentDialog(string photoPath, int brightness, int contrast)
    {
        InitializeComponent();
        _baseImage = PhotoItemViewModel.LoadBitmap(photoPath, 300);
        BrightnessSlider.Value = brightness;
        ContrastSlider.Value   = contrast;
        UpdatePreview();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();

    private void UpdatePreview()
    {
        PreviewImage.Source = PhotoItemViewModel.ApplyAdjustments(
            _baseImage, (int)BrightnessSlider.Value, (int)ContrastSlider.Value);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        BrightnessSlider.Value = 0;
        ContrastSlider.Value   = 0;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Brightness = (int)BrightnessSlider.Value;
        Contrast   = (int)ContrastSlider.Value;
        DialogResult = true;
    }
}
