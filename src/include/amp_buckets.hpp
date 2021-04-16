#pragma once
#include <stdint.h>
#include <assert.h>

#include "amp_types.hpp"
#include "amp_span.hpp"
#include "amp_buckets.h"

namespace amp {
	class amp_bucket;
}

struct amp_bucket_t
{
	struct amp_bucket_type_t
	{

	};

protected:
	const amp_bucket_type_t* type;
	amp_allocator_t* allocator;

	AMP__PUBLIC_ACCESSOR_DECLARE(amp_bucket)
};

namespace amp
{

	class amp_bucket : public amp_bucket_t
	{
	protected:
		amp_bucket(const amp_bucket_type_t* bucket_type, amp_allocator_t *bucket_allocator)
		{
			AMP_ASSERT(bucket_type && bucket_allocator);

			type = bucket_type;
			allocator = bucket_allocator;
		}

	public:
		virtual void destroy(amp_pool_t* scratch_pool)
		{
			AMP_UNUSED(scratch_pool);
			amp_allocator_free(this, allocator);
		}

	public:
		constexpr const amp_bucket_type_t* get_bucket_type() const
		{
			return type;
		}

		/**
		 * Read (and consume) up to @a requested bytes from @a bucket.
		 *
		 * A pointer to the data will be returned in @a data, and its length
		 * is specified by @a len.
		 *
		 * The data will exist until one of two conditions occur:
		 *
		 * 1) this bucket is destroyed
		 * 2) another call to any read function, get_remaining(), skip, reset or to peek()
		 *
		 * If an application needs the data to exist for a longer duration,
		 * then it must make a copy.
		 */
		virtual amp_err_t* read(
				amp_span* data,
				ptrdiff_t requested,
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
		virtual amp_err_t* read_until_eol(
					amp_span* data,
					amp_newline_t* found,
					amp_newline_t acceptable,
					ptrdiff_t requested,
					amp_pool_t* scratch_pool);

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
		virtual amp_err_t* read_iovec(
				int* vecs_used,
				ptrdiff_t requested,
				struct iovec* vecs,
				int vecs_count,
				amp_pool_t* scratch_pool)
		{
			AMP_ASSERT(vecs && vecs_count > 0);

			if (vecs_count <= 0)
			{
				*vecs_used = 0;
				return AMP_NO_ERROR;
			}

			amp_span result;
			AMP_ERR(read(&result, requested, scratch_pool));

			*vecs_used = 1;
			vecs[0].iov_base = const_cast<char*>(result.data());
			vecs[0].iov_len = result.size_bytes();
			return AMP_NO_ERROR;
		}

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
		virtual amp_err_t* read_bucket(
				amp_bucket_t** result,
				const char* bucket_type,
				amp_pool_t* scratch_pool)
		{
			AMP_UNUSED(scratch_pool);
			AMP_UNUSED(bucket_type);

			*result = nullptr;
			return AMP_NO_ERROR;
		}

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
		virtual amp_err_t* peek(
				amp_span* data,
				bool no_poll,
				amp_pool_t* scratch_pool)
		{
			AMP_UNUSED(scratch_pool);
			AMP_UNUSED(no_poll);

			*data = amp_span("", 0);
			return AMP_NO_ERROR;
		}

		/**
		 * @brief Tries to read @a requested bytes, without actually looking at them
		 * @param skipped
		 * @param requested
		 * @param scratch_pool
		 * @return
		*/
		virtual amp_err_t* read_skip(
			amp_off_t* skipped,
			amp_off_t requested,
			amp_pool_t* scratch_pool
		)
		{
			amp_span result;

			if (requested > INTPTR_MAX)
				requested = INTPTR_MAX;

			AMP_ERR(read(&result, (ptrdiff_t)requested, scratch_pool));

			*skipped = result.size_bytes();
			return AMP_NO_ERROR;
		}

		/**
		 * Returns length of remaining data to be read in @a bucket. Returns
		 * AMP_ERR_NOT_IMPLEMENTED if length is unknown.
		 */
		virtual amp_err_t*
			read_remaining_bytes(
					amp_off_t* remaining,
					amp_pool_t* scratch_pool)
		{
			AMP_UNUSED(scratch_pool);
			*remaining = -1;

			return amp_err_create(AMP_ERR_NOT_IMPLEMENTED, nullptr, nullptr);
		}

		/**
		 * @brief Resets the bucket to its original location *or* returns
		 * an error.
		 *
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented
		*/
		virtual amp_err_t* reset(amp_pool_t* scratch_pool)
		{
			AMP_UNUSED(scratch_pool);

			return amp_err_create(AMP_ERR_NOT_IMPLEMENTED, nullptr, nullptr);
		}

		/**
		 * @brief Duplicates the stream to two streams that will now return
		 * the same data. Will fail if the stream doesn't support this natively
		 *
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented.
		*/
		virtual amp_err_t* duplicate(amp_pool_t* scratch_pool)
		{
			AMP_UNUSED(scratch_pool);

			return amp_err_create(AMP_ERR_NOT_IMPLEMENTED, nullptr, nullptr);
		}

	public:
		class amp_bucket_type final : public amp_bucket_type_t
		{
			const char* name;
		public:
			amp_bucket_type(const char *type_name)
			{
				AMP_ASSERT(type_name);
				name = type_name;
			}
		};
	};


	class amp_bucket_file final : public amp_bucket
	{
	private:
		amp_file_t* file;
		amp_off_t file_remaining;
		ptrdiff_t available;
		ptrdiff_t position;
		span<char> buffer;

	private:
		amp_err_t* refill(ptrdiff_t requested);
	public:
		amp_bucket_file(amp_file_t* file, amp_allocator_t* bucket_allocator);

	public:
		virtual amp_err_t* read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_until_eol(
			amp_span* data,
			amp_newline_t* found,
			amp_newline_t acceptable,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* reset(amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_skip(
			amp_off_t* skipped,
			amp_off_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_remaining_bytes(
				amp_off_t* remaining,
				amp_pool_t* scratch_pool) override;

	public:
		virtual void destroy(amp_pool_t* pool) override;
	};
}

AMP__PUBLIC_ACCESSOR_INPLEMENT(amp_bucket)
