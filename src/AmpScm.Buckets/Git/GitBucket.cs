using System.Threading.Tasks;

namespace Amp.Buckets.Git
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
