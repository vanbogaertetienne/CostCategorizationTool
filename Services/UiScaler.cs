namespace CostCategorizationTool.Services;

/// <summary>
/// HiDPI-aware sizing helpers for hand-coded WinForms UIs.
///
/// TextRenderer.MeasureText with point-based fonts already returns device-pixel values
/// at the current screen DPI.  When AutoScaleMode.Dpi is enabled, WinForms additionally
/// scales all Bounds by DpiX/96, which would double-scale widths calculated via
/// TextRenderer.  BW() divides by the DPI scale first so AutoScaleMode gets a
/// 96-DPI-normalised value and scales it back to the correct device size at layout time.
/// </summary>
internal static class UiScaler
{
    private static float? _dpiScale;

    /// <summary>Ratio of primary-screen DPI to the 96-DPI design baseline.</summary>
    public static float DpiScale
    {
        get
        {
            if (_dpiScale is null)
            {
                using var g = Graphics.FromHwnd(IntPtr.Zero);
                _dpiScale = g.DpiX / 96f;
            }
            return _dpiScale.Value;
        }
    }

    /// <summary>
    /// Returns a 96-DPI-normalised button width (logical pixels) for use in
    /// Form/UserControl constructors that have AutoScaleMode.Dpi enabled.
    /// AutoScaleMode will scale this value to the correct device size at runtime.
    /// </summary>
    public static int BW(string text, Font font, int hPad = 24) =>
        (int)(TextRenderer.MeasureText(text, font).Width / DpiScale) + hPad;
}
