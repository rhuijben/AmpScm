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
		amp_bucket(const amp_bucket_type_t* bucket_type, amp_allocator_t* bucket_allocator)
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
				const amp_bucket_type_t* bucket_type,
				amp_pool_t* scratch_pool);

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
				amp_pool_t* scratch_pool);

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
			amp_pool_t* scratch_pool);

		/**
		 * Returns length of remaining data to be read in @a bucket. Returns
		 * AMP_ERR_NOT_SUPPORTED if length is unknown.
		 */
		virtual amp_err_t*
			read_remaining_bytes(
					amp_off_t* remaining,
					amp_pool_t* scratch_pool);

		/**
		 * @brief Resets the bucket to its original location *or* returns
		 * an error.
		 *
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented
		*/
		virtual amp_err_t*
			reset(amp_pool_t* scratch_pool);

		/**
		 * @brief Duplicates the stream to two streams that will now return
		 * the same data. Will fail if the stream doesn't support this natively
		 *
		 * Callers may assume that nothing changes to the stream itself
		 * if the function returns an error implying that the function
		 * is not implemented.
		*/
		virtual amp_err_t* duplicate(
			amp_bucket_t** result,
			bool for_reset,
			amp_pool_t* scratch_pool);

		/**
		 * @brief Gets the current byte position within the bucket or -1 if not available
		 * @return
		*/
		virtual amp_off_t get_position()
		{
			return -1;
		}

	public:
		class amp_bucket_type final : public amp_bucket_type_t
		{
			const char* name;
		public:
			amp_bucket_type(const char* type_name)
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

		virtual amp_off_t get_position() override;

	public:
		virtual void destroy(amp_pool_t* pool) override;
	};

	class amp_bucket_aggregate final : public amp_bucket
	{
	private:
		struct bucket_list_t
		{
			struct bucket_list_t* prev;
			struct bucket_list_t* next;
			amp_bucket_t* bucket;
		};

		bucket_list_t* first;
		bucket_list_t* last;
		bucket_list_t* cur;
		bool keep_open;
	public:
		amp_bucket_aggregate(bool keep_open, amp_allocator_t* allocator);

		void append(amp_bucket_t* bucket);
		void prepend(amp_bucket_t* bucket);

		virtual void destroy(amp_pool_t* scratch_pool) override;

	private:
		void cleanup(amp_pool_t* pool);

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

		virtual amp_err_t* read_bucket(
			amp_bucket_t** result,
			const amp_bucket_type_t* bucket_type,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_skip(
			amp_off_t* skipped,
			amp_off_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_remaining_bytes(
				amp_off_t* remaining,
				amp_pool_t* scratch_pool) override;

		virtual amp_err_t* reset(
				amp_pool_t* scratch_pool) override;

		virtual amp_err_t* duplicate(
			amp_bucket_t** new_bucket,
			bool for_reset,
			amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_simple abstract : public amp_bucket
	{
	protected:
		amp_span buffer;
		ptrdiff_t offset;

	protected:
		amp_bucket_simple(const amp_bucket_type_t* type, amp_span span, amp_allocator_t* allocator);

	public:
		virtual amp_err_t* read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override final;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override final;

		virtual amp_err_t* read_skip(
			amp_off_t* skipped,
			amp_off_t requested,
			amp_pool_t* scratch_pool) override final;

		virtual amp_err_t* read_remaining_bytes(
			amp_off_t* remaining,
			amp_pool_t* scratch_pool) override final;

		virtual amp_err_t* reset(
			amp_pool_t* scratch_pool) override final;

		virtual amp_err_t* duplicate(
			amp_bucket_t** new_bucket,
			bool for_reset,
			amp_pool_t* scratch_pool) override final;

		virtual amp_off_t get_position() override;
	};

	class amp_bucket_simple_copy : public amp_bucket_simple
	{
	public:
		amp_bucket_simple_copy(amp_span span, amp_allocator_t* allocator);
		virtual void destroy(amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_simple_own : public amp_bucket_simple
	{
	public:
		amp_bucket_simple_own(amp_span span, amp_allocator_t* allocator);
		virtual void destroy(amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_simple_const : public amp_bucket_simple
	{
	public:
		amp_bucket_simple_const(amp_span span, amp_allocator_t* allocator);
		virtual void destroy(amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_compression abstract : public amp_bucket
	{
	protected:
		amp_bucket_t* wrapped;
		bool read_eof;
		amp_compression_algorithm_t algorithm;
		amp_span read_buffer;
		ptrdiff_t read_position;
		amp_off_t position;

		amp::span<char> write_buffer;
		ptrdiff_t write_position;
		ptrdiff_t write_read_position;
		void* p0;

	protected:
		amp_bucket_compression(const amp_bucket_type_t* bucket_type, amp_bucket_t* wrapped_bucket, ptrdiff_t buffer_size, amp_allocator_t* allocator);
		virtual amp_err_t*
			init() = 0;
		virtual void
			done() = 0;

		virtual amp_err_t*
			refill(ptrdiff_t requested, amp_pool_t* scratch_pool) = 0;
	public:
		virtual amp_err_t* read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override;

		virtual amp_off_t get_position() override;

	public:
		virtual void destroy(amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_decompress : public amp_bucket_compression
	{
	public:
		amp_bucket_decompress(
			amp_bucket_t* wrapped_bucket,
			amp_compression_algorithm_t compression_algorithm,
			ptrdiff_t bufsize,
			amp_allocator_t* allocator);

	protected:
		virtual amp_err_t*
			init() override;
		virtual void
			done() override;

		virtual amp_err_t*
			refill(ptrdiff_t requested, amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_compress : public amp_bucket_compression
	{
	private:
		int compression_level;
	public:
		amp_bucket_compress(
			amp_bucket_t* wrapped_bucket,
			amp_compression_algorithm_t compression_algorithm,
			ptrdiff_t bufsize,
			int level,
			amp_allocator_t* allocator);

	protected:
		virtual amp_err_t*
			init() override;
		virtual void
			done() override;

		virtual amp_err_t*
			refill(ptrdiff_t requested, amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_hash : public amp_bucket
	{
	private:
		amp_bucket_t* wrapped;
		amp_hash_algorithm_t algorithm;
		amp_hash_result_t* new_result;
		amp_hash_result_t* expected_result;
		void* p1, * p2, * p3; // CNG or OpenSSL state
		size_t p3sz;
		bool done;
		amp_off_t hashed_bytes;

	public:
		amp_bucket_hash(amp_bucket_t* wrap_bucket,
						amp_hash_result_t* fill_result,
						const amp_hash_result_t* expect_result,
						amp_allocator_t* allocator);

		virtual void destroy(amp_pool_t* scratch_pool) override;
	private:
		void setupHashing();
		amp_err_t*
			finishHashing(bool useResult);
		void
			hashData(amp_span span);

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

		virtual amp_err_t* reset(
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t*
			read_remaining_bytes(
				amp_off_t* remaining,
				amp_pool_t* scratch_pool) override;

		virtual amp_off_t
			get_position() override;

		virtual amp_err_t*
			duplicate(
				amp_bucket_t** result,
				bool for_reset,
				amp_pool_t* scratch_pool);
	};

	class amp_bucket_limit final : public amp_bucket
	{
	private:
		amp_bucket_t* wrapped;
		amp_off_t position;
		amp_off_t end_offset;
		amp_off_t remaining;

	public:
		amp_bucket_limit(
			amp_bucket_t* wrap_bucket,
			amp_off_t limit,
			amp_allocator_t* allocator);

		virtual void destroy(amp_pool_t* scratch_pool) override;

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

		virtual amp_err_t* reset(
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t*
			read_remaining_bytes(
				amp_off_t* remaining,
				amp_pool_t* scratch_pool) override;

		virtual amp_off_t
			get_position() override;

		virtual amp_err_t* duplicate(
			amp_bucket_t** result,
			bool for_reset,
			amp_pool_t* scratch_pool) override;
	};

	class amp_bucket_block final : public amp_bucket
	{
	private:
		amp_bucket_t* wrapped;
	public:
		amp_bucket_block(amp_bucket_t* wrapped_bucket, amp_allocator_t* allocator);

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

		virtual amp_err_t* read_bucket(
				amp_bucket_t** result,
				const amp_bucket_type_t* bucket_type,
				amp_pool_t* scratch_pool) override;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_skip(
			amp_off_t* skipped,
			amp_off_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t*
			read_remaining_bytes(
				amp_off_t* remaining,
				amp_pool_t* scratch_pool) override;

		virtual amp_err_t* reset(amp_pool_t* scratch_pool) override;

		virtual amp_err_t* duplicate(
			amp_bucket_t** result,
			bool for_reset,
			amp_pool_t* scratch_pool) override;

		virtual amp_off_t get_position() override;
	};
}

AMP__PUBLIC_ACCESSOR_INPLEMENT(amp_bucket)
