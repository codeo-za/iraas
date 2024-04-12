namespace IRAAS.ImageProcessing
{
    public static class TimingHeaders
    {
        public const string PREFIX = "IRAAS-Timing-";
        public static readonly string Fetch = $"{PREFIX}Fetch";
        public static readonly string SourceFormatDetection = $"{PREFIX}Source-Format-Detection";
        public static readonly string OutputAutoFormatDetection = $"{PREFIX}Output-Auto-Format-Detection";
        public static readonly string LoadSource = $"{PREFIX}Load-Source";
        public static readonly string Resize = $"{PREFIX}Resize";
        public static readonly string EncodeOutput = $"{PREFIX}Encode-Output";
    }
}