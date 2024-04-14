using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using System.Numerics;

[PluginName("Mablet Mapping"), DeviceHub()]
public class MabletMapping : IPositionedPipelineElement<IDeviceReport>
{
    public PipelinePosition Position => PipelinePosition.PreTransform;

    public event Action<IDeviceReport>? Emit;

    [Property("Wrist Radius"), DefaultPropertyValue(5f), Unit("in"), ToolTip
    (
        "Default: 5.0\n\n" +
        "The radius of your wrist in inches.\n" +
        "Fine-tune this value so that making a sweeping horizontal motion with your wrist creates a straight line.\n" +
        "u-shape == radius set too low, increase value\n" +
        "n-shape == radius set too high, reduce value\n" +
        "flat line == radius set perfectly"
    )]
    public float wRadius { get; set; }

    [Property("Sensor Offset"), DefaultPropertyValue(0f), Unit("in"), ToolTip
    (
        "Default: 0.0\n\n" +
        "Offset from the would-be sensor to the pen tip in inches.\n" +
        "Change this if you did not put the pen right above where the sensor should go."
    )]
    public float sOffset { get; set; }

    [Property("DPI"), DefaultPropertyValue(800), ToolTip
    (
        "Default: 800\n\n" +
        "DPI of your mouse."
    )]
    public int dpiX { get; set; }

    [Property("X/Y Ratio"), DefaultPropertyValue(1f), ToolTip
    (
        "Default: 1.0\n\n" +
        "Aspect ratio of your DPI.\n" +
        "Higher means faster up to down movements."
    )]
    public float ratio { get; set; }

    [Property("Sensitivity"), DefaultPropertyValue(1f), ToolTip
    (
        "Default: 1.0\n\n" +
        "Multiplies the DPI value.\n\n" +
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

    public void Consume(IDeviceReport value)
    {
        if (AbsoluteOutput == null) ResolveOutputMode();
        if (value is not ITabletReport report || TabletReference == null || AbsoluteOutput == null) return;

        float x = report.Position.X, y = report.Position.Y;
        var digitizer = TabletReference.Properties.Specifications.Digitizer;
        Area output = AbsoluteOutput.Output;
        Area input = AbsoluteOutput.Input;
        float upmm = digitizer.MaxX / digitizer.Width;
        float upi = mmpi * upmm;

        float mx = input.Width * upmm, my = input.Height * upmm;
        float ax = input.Position.X * upmm, ay = input.Position.Y * upmm;
        float rot = input.Rotation * rpd;

        float radius = wRadius * upi;
        float offset = (wRadius + sOffset) * upi;

        float mousex = dpiX * sens / upi;
        float mousey = mousex * ratio;

        float cosAngle = MathF.Cos(rot), sinAngle = MathF.Sin(rot);

        float changex = x - (ax - sinAngle * offset);
        float changey = y - (ay + cosAngle * offset);

        float outputX = MathF.Atan((changey * sinAngle - changex * cosAngle) / (changey * cosAngle + changex * sinAngle)) * radius * mousex / output.Width * mx;
        float outputY = (offset - MathF.Sqrt(changex * changex + changey * changey)) * mousey / output.Height * my;

        report.Position = new Vector2(
            ax + outputX * cosAngle - outputY * sinAngle,
            ay + outputX * sinAngle + outputY * cosAngle
        );

        value = report;
        Emit?.Invoke(value);
    }

    private float rpd = MathF.PI / 180f;
    private float mmpi = 25.4f;

    [TabletReference]
    public TabletReference? TabletReference { get; set; }

    [Resolved]
    public IDriver? driver;
    public AbsoluteOutputMode? AbsoluteOutput;
    public void ResolveOutputMode()
    {
        if (driver is Driver drv)
        {
            IOutputMode output = drv.InputDevices
                .Where(dev => dev?.OutputMode?.Elements?.Contains(this) ?? false)
                .Select(dev => dev?.OutputMode).FirstOrDefault();

            if (output is AbsoluteOutputMode abs_output)
            {
                AbsoluteOutput = abs_output;
                return;
            }
        }
    }
}
