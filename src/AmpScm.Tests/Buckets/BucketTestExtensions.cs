using Amp.Buckets;

namespace Amp.BucketTests.Buckets
{
    public static class BucketTestExtensions
    {
        public static Bucket PerByte(this Bucket self)
        {
            return new PerByteBucket(self);
        }
    }
}
