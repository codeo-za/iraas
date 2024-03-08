using NSubstitute;
using PeanutButter.RandomGenerators;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace IRAAS.Tests.ImageProcessing
{
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
                .WithValidMaxImageFetchTime();
        }

        public IAppSettingsBuilder WithValidMaxImageFetchTime()
        {
            return WithProp(
                o => o.MaxImageFetchTimeInMilliseconds.Returns(GetRandomInt(1000, 2000))
            );
        }

        public IAppSettingsBuilder WithNonZeroConcurrency()
        {
            return WithProp(o => o.MaxConcurrency.Returns(GetRandomInt(1)));
        }
    }

}