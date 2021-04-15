#pragma once
#include "amp_types.h"

#ifdef _WIN32
/* Use same define as APR to avoid collisions in any order */
#ifndef APR_IOVEC_DEFINED
#define APR_IOVEC_DEFINED
struct iovec
{
	void *iov_base;
	size_t iov_len;
};
#endif /* !APR_IOVEC_DEFINED */


#else
#include <sys/types.h>
#include <sys/uio.h>
#include <unistd.h>
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define SERF_ERROR_WAIT_CONN 99921919 // TODO: Replace with something sane

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

#ifdef __cplusplus
}
#endif
