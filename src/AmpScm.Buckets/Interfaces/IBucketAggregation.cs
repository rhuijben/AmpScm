namespace AmpScm.Buckets.Interfaces
{
    interface IBucketAggregation
    {
        Bucket Append(Bucket bucket);
        Bucket Prepend(Bucket bucket);
    }
}
