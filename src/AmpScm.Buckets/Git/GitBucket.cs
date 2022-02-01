using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    public abstract class GitBucket : Specialized.WrappingBucket
    {
        protected GitBucket(Bucket inner) : base(inner)
        {
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            return base.DuplicateAsync(reset);
        }
    }
}
