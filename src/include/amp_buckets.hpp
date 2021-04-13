#pragma once
#include <assert.h>

#include "amp_types.hpp"
#include "amp_span.hpp"
#include "amp_buckets.h"

struct amp_bucket_t
{

};

namespace amp
{
	

	class amp_bucket : public amp_pool_managed, public amp_bucket_t
	{
	public:
		virtual const char* get_bucket_type();

		/**
		 * Read (and consume) up to @a requested bytes from @a bucket.
		 *
		 * A pointer to the data will be returned in @a data, and its length
		 * is specified by @a len.
		 *
		 * The data will exist until one of two conditions occur:
		 *
		 * 1) this bucket is destroyed
		 * 2) another call to any read function, get_remaining() or to peek()
		 *
		 * If an application needs the data to exist for a longer duration,
		 * then it must make a copy.
		 */
		virtual amp_error_t* read(
				amp_span* data,
				size_t requested,
				amp_pool_t* scratch_pool) = 0;

		/**
		 * Read (and consume) a line of data from @a bucket.
		 *
		 * The acceptable forms of a newline are given by @a acceptable, and
		 * the type found is returned in @a found. If a newline is not present
		 * in the returned data, then SERF_NEWLINE_NONE is stored into @a found.
		 *
		 * A pointer to the data is returned in @a data, and its length is
		 * specified by @a len. The data will include the newline, if present.
		 *
		 * Note that there is no way to limit the amount of data returned
		 * by this function. @see serf_bucket_limited_readline().
		 *
		 * The lifetime of the data is the same as that of the @see read
		 * function above.
		 */
		virtual amp_error_t* read_line(
					amp_span * data,
					amp_newline_t* found,
					amp_newline_t newline_t,
					size_t limit,
					amp_pool_t* scratch_pool
		);

		/**
		 * Read a set of pointer/length pairs from the bucket.
		 *
		 * The size of the @a vecs array is specified by @a vecs_size. The
		 * bucket should fill in elements of the array, and return the number
		 * used in @a vecs_used.
		 *
		 * Each element of @a vecs should specify a pointer to a block of
		 * data and a length of that data.
		 *
		 * The total length of all data elements should not exceed the
		 * amount specified in @a requested.
		 *
		 * The lifetime of the data is the same as that of the @see read
		 * function above.
		 */
		virtual amp_error_t* read_iovec(
				int* vecs_used,
				size_t requested,
				struct iovec* vecs,
				int vecs_count
		);

		/**
		 * Look within @a bucket for a bucket of the given @a type. The bucket
		 * must be the "initial" data because it will be consumed by this
		 * function. If the given bucket type is available, then read and consume
		 * it, and return it to the caller.
		 *
		 * This function is usually used by readers that have custom handling
		 * for specific bucket types (e.g. looking for a file bucket to pass
		 * to apr_socket_sendfile).
		 *
		 * If a bucket of the given type is not found, then NULL is returned.
		 *
		 * The returned bucket becomes the responsibility of the caller. When
		 * the caller is done with the bucket, it should be destroyed.
		 */
		virtual amp_error_t* read_bucket(
				amp_bucket_t** result,
				const char* bucket_type,
				amp_pool_t* scratch_pool
		);

		/**
		 * Peek, but don't consume, the data in @a bucket.
		 *
		 * Since this function is non-destructive, the implicit read size is
		 * read all remaining. The caller can then use whatever amount is
		 * appropriate.
		 *
		 * The @a data parameter will point to the data, and @a len will
		 * specify how much data is available. The lifetime of the data follows
		 * the same rules as the @see read function above.
		 *
		 * Note: if the peek does not return enough data for your particular
		 * use, then you must read/consume some first, then peek again.
		 *
		 * If the returned data represents all available data, then APR_EOF
		 * will be returned. Since this function does not consume data, it
		 * can return the same data repeatedly rather than blocking; thus,
		 * APR_EAGAIN will never be returned.
		 */
		virtual amp_error_t* peek(
				amp_span* data,
				bool no_poll,
				amp_pool_t* scratch_pool
		);

		/**
		 * Returns length of remaining data to be read in @a bucket. Returns
		 * AMP_ERR_NOT_IMPLEMENTED if length is unknown.
		 * 
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented.
		 */
		virtual amp_error_t* get_remaining(
				ptrdiff_t* remaining,
				amp_pool_t* scratch_pool
		);

		/**
		 * @brief Resets the bucket to its original location *or* returns
		 * an error.
		 * 
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented
		*/
		virtual amp_error_t* reset(amp_pool_t* scratch_pool);

		/**
		 * @brief Duplicates the stream to two streams that will now return
		 * the same data. Will fail if the stream doesn't support this natively
		 * 
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented.
		*/
		virtual amp_error_t* duplicate(amp_pool_t* scratch_pool);
	};
}