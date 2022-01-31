﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amp.Buckets.Specialized;

namespace Amp.Buckets
{
    [Flags]
    public enum BucketEol
    {
        None        = 0x00,
        LF          = 0x01,
        CR          = 0x02,
        CRLF        = 0x04,
        Zero        = 0x08,

        EolMask     = 0xFF,
        CRSplit   = 0x100000
    }
    partial class Bucket
    {
        public async virtual ValueTask<(BucketBytes, BucketEol)> ReadUntilEolAsync(BucketEol acceptableEols, int requested = int.MaxValue)
        {
            if ((acceptableEols & ~BucketEol.EolMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(acceptableEols));

            using var pd = await this.PollAsync(1);

            if (pd.IsEof)
                return (BucketBytes.Eof, BucketEol.None);

            requested = CalculateEolReadLength(acceptableEols, requested, pd.Data.Span, out var single_cr_requested);

            var read = await pd.ReadAsync(requested);
            var found = GetEolResult(acceptableEols, requested, pd.Length, single_cr_requested, read.Span);

            return (read, found);
        }

        private static int CalculateEolReadLength(BucketEol acceptableEols, int requested, ReadOnlySpan<byte> buffer, out bool single_cr_requested)
        {
            int cr = (0 != (acceptableEols & (BucketEol.CR | BucketEol.CRLF))) ? buffer.IndexOf((byte)'\r') : -1;
            int lf = (0 != (acceptableEols & BucketEol.LF)) ? buffer.IndexOf((byte)'\n') : -1;
            int zr = (0 != (acceptableEols & BucketEol.Zero)) ? buffer.IndexOf((byte)'\0') : -1;

            // Fold zero in lf
            lf = (lf >= 0 && zr >= 0) ? Math.Min(lf, zr) : (lf >= 0 ? lf : zr);

            if (cr >= 0 && (acceptableEols & (BucketEol.CR | BucketEol.CRLF)) == BucketEol.CRLF)
            {
                // If we have a cr but not a cr+lf, we want to check the next cr (if any)
                while (cr >= 0 && (lf < 0 || cr < lf) && (cr + 1 < buffer.Length) && buffer[cr + 1] != '\n')
                {
                    var s = buffer.Slice(cr + 1);

                    int n = s.IndexOf((byte)'\r');

                    if (n >= 0)
                        cr += 1 + n;
                    else
                        cr = -1;
                }
            }

            // fold lf (and zero) in cr
            cr = (cr >= 0 && lf >= 0) ? Math.Min(cr, lf) : (cr >= 0 ? cr : lf);

            int linelen = cr;
            single_cr_requested = false;

            if (cr >= 0
                && buffer[cr] == '\r'
                && (acceptableEols & BucketEol.CRLF) != 0
                && linelen + 1 < buffer.Length)
            {
                if (buffer[cr + 1] == '\n')
                    requested = linelen + 2; // cr+lf
                else if ((acceptableEols & BucketEol.CRLF) != 0)
                {
                    requested = linelen + 1; // cr without lf
                    single_cr_requested = true;
                }
                else
                {
                    // easy out. Just include the single character after the cr
                    requested = linelen + 2; // cr+lf
                }
            }
            else if (cr >= 0)
            {
                requested = linelen + 1;
            }
            else if (acceptableEols == BucketEol.CRLF)
                requested = Math.Min(buffer.Length + 2, requested); // No newline in rq_len, and we need 2 chars for eol
            else
                requested = Math.Min(buffer.Length + 1, requested); // No newline in rq_len, and we need 1 char for eol

            return requested;
        }

        private static BucketEol GetEolResult(BucketEol acceptableEols, int requested, int pdLength, bool single_cr_requested, ReadOnlySpan<byte> read)
        {
            BucketEol found;

            if (read.IsEmpty /* || read.IsEof */)
                return BucketEol.None;

            if (0 != (acceptableEols & BucketEol.CRLF)
                     && read.Length >= 2 && read[read.Length - 1] == '\n' && read[read.Length - 2] == '\r')
            {
                found = BucketEol.CRLF;
            }
            else if (0 != (acceptableEols & BucketEol.LF) && read[read.Length - 1] == '\n')
            {
                found = BucketEol.LF;
            }
            else if (BucketEol.CR != (acceptableEols & BucketEol.CR | BucketEol.CRLF) && read[read.Length - 1] == '\r')
            {
                found = BucketEol.CR;
            }
            else if (0 != (acceptableEols & BucketEol.CRLF) && read[read.Length - 1] == '\r')
            {
                if (single_cr_requested && requested == pdLength)
                    found = BucketEol.CR;
                else
                    found = BucketEol.CRSplit;
            }
            else if (0 != (acceptableEols & BucketEol.Zero) && read[read.Length - 1] == '\0')
            {
                found = BucketEol.Zero;
            }
            else
            {
                found = BucketEol.None;
            }

            return found;
        }

    }
}
