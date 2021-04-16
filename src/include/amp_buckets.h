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


#define SERF_ERROR_WAIT_CONN 99921919 // TODO: Replace with something sane

#define AMP_READ_ALL_AVAIL MAXINT_PTR

#define AMP_IS_BUCKET_READ_ERROR(err) ((err) \
                                        && !AMP_ERR_IS_EOF(err) \
                                        && !AMP_ERR_IS_EAGAIN(err) \
                                        && (SERF_ERROR_WAIT_CONN != err->status))


AMP_DECLARE(amp_err_t*)
amp_bucket_read(
	const char** data,
	size_t* data_len,
	amp_bucket_t* bucket,
	size_t requested,
	amp_pool_t* scratch_pool);

AMP_DECLARE(amp_err_t*)
amp_bucket_read_eol(
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

AMP_C__END
