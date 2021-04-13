#pragma once
#include "amp_types.h"
#ifdef __cplusplus
extern "C" {
#endif

	AMP_DECLARE(amp_error_t*)
		amp_bucket_read(
			const char** data,
			size_t* data_len,
			amp_bucket_t* bucket,
			size_t requested,
			amp_pool_t* scratch_pool);

#ifdef __cplusplus
}
#endif
