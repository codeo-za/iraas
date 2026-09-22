using NSubstitute;
using PeanutButter.RandomGenerators;

namespace IRAAS.Tests.ImageProcessing;

// ReSharper disable once InconsistentNaming
// ReSharper disable once UnusedType.Global
public class IAppSettingsBuilder
    : GenericBuilder<IAppSettingsBuilder, IAppSettings>
{
    public override IAppSettings ConstructEntity()
    {
        return Substitute.For<IAppSettings>();
    }

    public override IAppSettingsBuilder WithRandomProps()
    {
        return base.WithRandomProps()
            .WithNonZeroConcurrency()
            .WithValidMaxImageFetchTime()
            .WithEmptyPostAuthTokens();
    }

    public IAppSettingsBuilder WithEmptyPostAuthTokens()
    {
        return WithProp(
            o => o.PostAuthTokens.Returns("")
        );
    }

    public IAppSettingsBuilder WithValidMaxImageFetchTime()
    {
        return WithProp(
            o => o.MaxImageFetchTimeInMilliseconds.Returns(RandomValueGen.GetRandomInt(1000, 2000))
        );
    }

    public IAppSettingsBuilder WithNonZeroConcurrency()
    {
        return WithProp(o => o.MaxConcurrency.Returns(RandomValueGen.GetRandomInt(1)));
    }
}