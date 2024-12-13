using DryIoc;

namespace IRAAS;

// ReSharper disable once ClassNeverInstantiated.Global
public class CompositionRoot
{
    public CompositionRoot(IContainer container)
    {
        var bootstrapper = new Bootstrapper();
        bootstrapper.Bootstrap(container);
    }
}