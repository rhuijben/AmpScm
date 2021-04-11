#pragma once
#include "amp_types.h"
#ifdef __cplusplus
extern "C" {
#endif

	AMP_DLL(amp_error_t*)
		amp_bucket_read(
			const char** data,
			amp_size_t* data_len,
			amp_bucket_t* bucket,
			amp_size_t requested,
			amp_pool_t* scratch_pool);

#ifdef __cplusplus
}
#endif
