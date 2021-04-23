#pragma once
#include "amp_types.h"

#ifndef _WIN32
/* For iovec, etc. */
#include <sys/types.h>
#include <sys/uio.h>
#include <unistd.h>
#endif

AMP_C__START

#ifdef _WIN32
/* Use same define as APR to avoid collisions in any order */
#ifndef APR_IOVEC_DEFINED
#define APR_IOVEC_DEFINED
struct iovec
{
	void* iov_base;
	size_t iov_len;
};
#endif /* !APR_IOVEC_DEFINED */
#endif

enum amp_hash_algorithm_t
{
	amp_hash_algorithm_none,
	amp_hash_algorithm_adler32,
	amp_hash_algorithm_crc32,
	amp_hash_algorithm_md5,
	amp_hash_algorithm_sha1,
	amp_hash_algorithm_sha256
};

enum amp_compression_algorithm_t
{
	amp_compression_algorithm_none,
	amp_compression_algorithm_deflate,	// deflate, no wrapping (http)
	amp_compression_algorithm_zlib,		// deflate, zlib wrapping (git)
	amp_compression_algorithm_gzip		// deflate, gzip wrapping (gzip)

	// TODO: Brotli?
};

typedef struct amp_hash_result_t
{
	size_t hash_bytes;
	amp_hash_algorithm_t hash_algorithm;
	amp_off_t original_size;
	unsigned char bytes[1];
} amp_hash_result_t;

amp_hash_result_t*
amp_hash_result_create(
	amp_hash_algorithm_t algorithm,
	amp_pool_t* result_pool);

const char*
amp_hash_result_to_cstring(
	amp_hash_result_t* result,
	amp_boolean_t for_display,
	amp_pool_t* result_pool);


#define AMP_ERR_WAIT_CONN 99921919 // TODO: Replace with something sane

#define AMP_READ_ALL_AVAIL MAXINT_PTR

#define AMP_IS_BUCKET_READ_ERROR(err) ((err) \
                                        && !AMP_ERR_IS_EOF(err) \
                                        && !AMP_ERR_IS_EAGAIN(err) \
                                        && (AMP_ERR_WAIT_CONN != err->status))


AMP_DECLARE(amp_err_t*)
amp_bucket_read(
	const char** data,
	size_t* data_len,
	amp_bucket_t* bucket,
	size_t requested,
	amp_pool_t* scratch_pool);

AMP_DECLARE(amp_err_t*)
amp_bucket_read_until_eol(
	const char** data,
	size_t* data_len,
	amp_newline_t* found,
	amp_bucket_t* bucket,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool);

AMP_DECLARE(void)
amp_bucket_destroy(
	amp_bucket_t* bucket,
	amp_pool_t* scratch_pool);

AMP_DECLARE(amp_bucket_t*)
amp_bucket_file_create(
	amp_file_t* file,
	amp_allocator_t* allocator,
	amp_pool_t* scratch_pool);


AMP_DECLARE(amp_bucket_t*)
amp_bucket_aggregate_create(amp_allocator_t* allocator);

AMP_DECLARE(void)
amp_bucket_aggregate_append(
		amp_bucket_t* aggregate,
		amp_bucket_t* to_prepend);

AMP_DECLARE(void)
amp_bucket_aggregate_append(
	amp_bucket_t* aggregate,
	amp_bucket_t* to_prepend);


AMP_DECLARE(amp_bucket_t*)
amp_bucket_simple_create(const void* data, ptrdiff_t size, amp_allocator_t* allocator);

AMP_DECLARE(amp_bucket_t*)
amp_bucket_simple_own_create(const void* data, ptrdiff_t size, amp_allocator_t* allocator);

AMP_DECLARE(amp_bucket_t*)
amp_bucket_simple_copy_create(const void* data, ptrdiff_t size, amp_allocator_t* allocator);

AMP_DECLARE(amp_err_t*)
amp_bucket_hash_create(
	amp_bucket_t** new_bucket,
	amp_hash_result_t** hash_result,
	amp_bucket_t* wrapped_bucket,
	amp_hash_algorithm_t algorithm,
	const amp_hash_result_t* expected_result,
	amp_allocator_t* allocator,
	amp_pool_t* result_pool); // result pool for hash_result

AMP_DECLARE(amp_err_t*)
amp_bucket_compress_create(
	amp_bucket_t** new_bucket,
	amp_bucket_t* to_compress,
	amp_compression_algorithm_t algorithm,
	int level,
	ptrdiff_t buffer_size,
	amp_allocator_t* allocator);

AMP_DECLARE(amp_err_t*)
amp_bucket_decompress_create(
	amp_bucket_t** new_bucket,
	amp_bucket_t* to_compress,
	amp_compression_algorithm_t algorithm,
	ptrdiff_t buffer_size,
	amp_allocator_t* allocator);

AMP_DECLARE(amp_bucket_t*)
amp_bucket_block_create(amp_bucket_t* wrapped,
						amp_allocator_t* allocator);

AMP_C__END
