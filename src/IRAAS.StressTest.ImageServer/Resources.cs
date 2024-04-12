using System.Collections.Concurrent;
using System.IO;

namespace IRAAS.StressTest.ImageServer
{
    public static class Resources
    {
        public static class Paths
        {
            public const string FLUFFY_CAT_BMP = "resources/fluffy-cat.bmp";
            public const string FLUFFY_CAT_JPEG = "resources/fluffy-cat.jpg";
        }

        public static class Data
        {
            public static byte[] FluffyCatBmp => GetResource(Paths.FLUFFY_CAT_BMP);
            public static byte[] FluffyCatJpeg => GetResource(Paths.FLUFFY_CAT_JPEG);
        }

        private static readonly ConcurrentDictionary<string, byte[]> ResourceData
            = new ConcurrentDictionary<string, byte[]>();

        private static byte[] GetResource(string path)
        {
            if (ResourceData.TryGetValue(path, out var data))
            {
                return data;
            }

            return ResourceData[path] = File.ReadAllBytes(path);
        }
    }
}