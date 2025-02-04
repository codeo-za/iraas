using PeanutButter.RandomGenerators;

namespace IRAAS.Tests.ImageProcessing;

// ReSharper disable once UnusedType.Global
public class AppSettingsBuilder
    : GenericBuilder<AppSettingsBuilder, AppSettings>
{
    public override AppSettingsBuilder WithRandomProps()
    {
        return base.WithRandomProps()
            .WithNonZeroConcurrency()
            .WithValidMaxImageFetchTime();
    }

    public AppSettingsBuilder WithValidMaxImageFetchTime()
    {
        return WithProp(
            o => o.MaxImageFetchTimeInMilliseconds = GetRandomInt(1000, 2000)
        );
    }

    public AppSettingsBuilder WithNonZeroConcurrency()
    {
        return WithProp(o => o.MaxConcurrency = GetRandomInt(1));
    }
}