using AmpScm.Buckets;

namespace AmpScm.BucketTests.Buckets
{
    public static class BucketTestExtensions
    {
        public static Bucket PerByte(this Bucket self)
        {
            return new PerByteBucket(self);
        }
    }
}
