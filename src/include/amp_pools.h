#pragma once

#include <apr_pools.h>
#include "amp_types.h"

#define AMP_VA_NULL ((void*)0)

#ifdef __cplusplus
extern "C" {
#endif
	typedef apr_pool_t amp_pool_t;

	AMP_DLL(amp_pool_t*)
		amp_pool_create_unmanaged(amp_boolean_t thread_safe);

	AMP_DLL(amp_pool_t*)
		amp_pool_create(amp_pool_t* parent_pool);

	AMP_DLL(amp_pool_t*)
		amp_pool_clear(amp_pool_t* pool);

	AMP_DLL(void)
		amp_pool_destroy(amp_pool_t* pool);

#ifdef __cplusplus
}
#endif
