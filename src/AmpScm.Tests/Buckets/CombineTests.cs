using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.BucketTests.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AmpScm.Tests.Buckets
{
    [TestClass]
    public class CombineTests
    {
        [TestMethod]
        public async Task VerifyXor()
        {
            var left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
            var right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

            var xOr = new BitwiseXorBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));


            Assert.IsTrue(xOr.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31), (x,y)=> x^y)));
        }

        [TestMethod]
        public async Task VerifyAnd()
        {
            var left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
            var right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

            var xOr = new BitwiseAndBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));


            Assert.IsTrue(xOr.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31), (x, y) => x & y)));
        }
    }
}
