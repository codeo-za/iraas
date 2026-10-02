using DryIoc;

namespace IRAAS;

// ReSharper disable once ClassNeverInstantiated.Global
public class CompositionRoot
{
    public CompositionRoot(
        IContainer container,
        IAppSettings appSettings
    )
    {
        var bootstrapper = new Bootstrapper();
        bootstrapper.Bootstrap(
            container,
            appSettings
        );
    }
}