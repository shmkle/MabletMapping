using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Devices;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using System.Numerics;

[PluginName("Mablet Mapping"), DeviceHub()]
public class MabletMapping : IPositionedPipelineElement<IDeviceReport>
{
    public PipelinePosition Position => PipelinePosition.PreTransform;

    public event Action<IDeviceReport>? Emit;

    [Property("Wirst Radius"), DefaultPropertyValue(5.0f), Unit("in"), ToolTip
    (
        "Default: 5.0\n\n" +
        "Radius of your wrist in intches"
    )]
    public float wRadius { get; set; }

    [Property("Sensor Offset"), DefaultPropertyValue(0.0f), Unit("in"), ToolTip
    (
        "Default: 0.0\n\n" +
        "Offset from the would be sensor to the pen tip in intches\n\n" +
        "Change this if you didn't put the pen right above where the sensor should go"
    )]
    public float sOffset { get; set; }

    [Property("DPI"), DefaultPropertyValue(800), ToolTip
    (
        "Default: 800\n\n" +
        "DPI of your mouse"
    )]
    public int dpiX { get; set; }

    [Property("X/Y Ratio"), DefaultPropertyValue(1.0f), ToolTip
    (
        "Default: 1.0\n\n" +
        "Aspect ratio of your DPI, higher means faster up to down movements."
    )]
    public float ratio { get; set; }

    [Property("Sensitivity Multiplier"), DefaultPropertyValue(1.0f), ToolTip
    (
        "Default: 1.0\n\n" +
        "Multiplies the X DPI and Y DPI values\n\n" +
        "Windows pointer speeds:\n" +
        "0.03125 (1st tick)\n" +
        "0.0625 (2nd tick)\n" +
        "0.25 (3rd tick)\n" +
        "0.5 (4th tick)\n" +
        "0.75 (5th tick)\n" +
        "1.0 (6th tick)\n" +
        "1.5 (7th tick)\n" +
        "2.0 (8th tick)\n" +
        "2.5 (9th tick)\n" +
        "3.0 (10th tick)\n" +
        "3.5 (11th tick)\n"
    )]
    public float sens { get; set; }

    [Property("Area Position X"), DefaultPropertyValue(50), Unit("%"), ToolTip
    (
        "Default: 50\n\n" +
        "The X postion of the areas center in percentage across the tablet"
    )]
    public int aposx { get; set; }

    [Property("Area Position Y"), DefaultPropertyValue(50), Unit("%"), ToolTip
(
    "Default: 50\n\n" +
    "The Y postion of the areas center in percentage across the tablet"
)]
    public int aposy { get; set; }

    [Property("Area Rotation"), DefaultPropertyValue(0), Unit("°"), ToolTip
    (
        "Default: 0\n\n" +
        "The rotation of the area in degrees"
    )]
    public int arot { get; set; }

    public float mmpi = 25.4f;

    public void Consume(IDeviceReport value)
    {
        if (TabletReference == null)
        {
            Log.Debug("MabletMapping", "tablet refrence does not exist");
            return;
        }

        if (absoluteOutput == null)
        {
            ResolveOutputMode();
            return;
        }

        if (value is ITabletReport report)
        {
            var digitizer = TabletReference.Properties.Specifications.Digitizer;

            float resx = absoluteOutput.Output.Width;
            float resy = absoluteOutput.Output.Height;
            float mx = digitizer.MaxX;
            float my = digitizer.MaxY;
            float upmm = mx / digitizer.Width;

            float rot = arot / 180.0f * MathF.PI;
            float ax = aposx / 100.0f * mx;
            float ay = aposy / 100.0f * my;

            float radius = wRadius * mmpi * upmm;
            float offset = (wRadius + sOffset) * mmpi * upmm;
            float dpiY = dpiX * ratio;

            float mousex = dpiX * sens / mmpi;
            float mousey = dpiY * sens / mmpi;

            float x = report.Position.X;
            float y = report.Position.Y;

            float changex = x - (ax + MathF.Sin(-rot) * offset);
            float changey = y - (ay + MathF.Cos(-rot) * offset);

            float outputX = mx * (0.5f + MathF.Atan((changey * MathF.Sin(rot) - changex * MathF.Cos(rot)) / (changey * MathF.Cos(rot) + changex * MathF.Sin(rot))) * radius / upmm * mousex / resx);
            float outputY = my * (0.5f + (offset - MathF.Pow(MathF.Pow(changex, 2.0f) + MathF.Pow(changey, 2.0f), 0.5f)) / upmm * mousey / resy);

            report.Position = new Vector2
            (
                outputX,
                outputY
            );

            value = report;
        }

        Emit?.Invoke(value);
    }

    [TabletReference]
    public TabletReference? TabletReference { get; set; }

    [Resolved]
    public IDriver? driver;
    private AbsoluteOutputMode? absoluteOutput;
    private void ResolveOutputMode()
    {
        if (driver is Driver drv)
        {
            IOutputMode output = drv.InputDevices
                .Where(dev => dev?.OutputMode?.Elements?.Contains(this) ?? false)
                .Select(dev => dev?.OutputMode).FirstOrDefault();

            if (output is AbsoluteOutputMode abs_output)
            {
                absoluteOutput = abs_output;
                return;
            }
        }
    }

    public IEnumerable<IDeviceEndpoint> GetDevices()
    {
        throw new NotImplementedException();
    }
}
