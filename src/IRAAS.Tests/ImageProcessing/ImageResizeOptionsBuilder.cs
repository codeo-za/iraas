using IRAAS.ImageProcessing;
using PeanutButter.RandomGenerators;

namespace IRAAS.Tests.ImageProcessing;

// ReSharper disable once UnusedType.Global
public class ImageResizeOptionsBuilder
    : GenericBuilder<ImageResizeOptionsBuilder, ImageResizeOptions>
{
    public override ImageResizeOptionsBuilder WithRandomProps()
    {
        return base.WithRandomProps()
            .WithRandomValidDevicePixelRatio();
    }

    public ImageResizeOptionsBuilder WithRandomValidDevicePixelRatio()
    {
        return WithDevicePixelRatio(GetRandomDecimal(1, 3));
    }

    public ImageResizeOptionsBuilder WithDevicePixelRatio(
        decimal devicePixelRatio
    )
    {
        return WithProp(o => o.DevicePixelRatio = devicePixelRatio);
    }
}
