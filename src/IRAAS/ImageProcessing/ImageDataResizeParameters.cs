namespace IRAAS.ImageProcessing;

public interface IDataImageResizeParameters : IActiveImageResizeParameters
{
    public byte[] ImageData { get; set; }
}

public class ImageDataResizeParameters 
    : ImageResizeParameters, IDataImageResizeParameters
{
    public byte[] ImageData { get; set; }
}