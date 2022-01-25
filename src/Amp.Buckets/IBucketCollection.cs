namespace Amp.Buckets
{
    interface IBucketAggregation
    {
        Bucket Append(Bucket bucket);
        Bucket Prepend(Bucket bucket);
    }

    public interface IBucketNoClose
    {
        Bucket NoClose();
    }

}
