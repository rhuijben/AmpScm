namespace Amp.Buckets.Specialized
{
    public enum BucketCompressionAlgorithm
    {
        Deflate,
        ZLib,
#if NETCOREAPP
        Brotli
#endif
    }
}
