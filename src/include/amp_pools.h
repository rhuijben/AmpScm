
#include "amp_types.h"

#pragma once

AMP_C__START

typedef void* (*amp_allocator_alloc_func_t)(size_t size);
typedef void (*amp_allocator_free_func_t)(void* data);
typedef void* (*amp_allocator_realloc_func_t)(void* data, size_t new_size, size_t original_size);
typedef void (*amp_allocator_abort_func_t)(void);
typedef int (*amp_pool_cleanup_func_t)(void* data);

AMP_DECLARE(amp_allocator_t*) amp_allocator_create(
	void);

AMP_DECLARE(amp_allocator_t*) amp_allocator_create_ex(
	amp_allocator_alloc_func_t alloc_func,
	amp_allocator_free_func_t free_func);

AMP_DECLARE(void*) amp_allocator_alloc(
	size_t size,
	amp_allocator_t* allocator);

AMP_DECLARE(void) amp_allocator_free(
	void* data,
	amp_allocator_t* allocator);

AMP_DECLARE(void) amp_allocator_destroy(
	amp_allocator_t* allocator);

/**
* Creates a subpool in @a pool
*/
AMP_DECLARE(amp_pool_t*) amp_pool_create(
	amp_pool_t* pool);

AMP_DECLARE(void) amp_pool_clear(
	amp_pool_t* pool);

AMP_DECLARE(void) amp_pool_destroy(
	amp_pool_t* pool);

AMP_DECLARE(amp_pool_t*) amp_pool_create_ex(
	amp_pool_t* in_pool,
	amp_allocator_t* allocator);

AMP_DECLARE(void) amp_pool_cleanup_register(
	amp_pool_t* pool,
	const void* data,
	amp_pool_cleanup_func_t plain_cleanup,
	amp_pool_cleanup_func_t child_cleanup);

AMP_DECLARE(void) amp_pool_cleanup_kill(
	amp_pool_t* pool,
	const void* data,
	amp_pool_cleanup_func_t plain_cleanup);

AMP_DECLARE(void) amp_pool_cleanup_run(
	amp_pool_t* pool);

AMP_DECLARE(void) amp_pool_cleanup_run_exec(
	amp_pool_t* pool);

AMP_DECLARE(amp_allocator_t*) amp_pool_get_allocator(
	amp_pool_t* pool);

AMP_DECLARE(void*) amp_palloc(
	size_t bytes,
	amp_pool_t* pool);

AMP_DECLARE(void*) amp_pcalloc(
	size_t bytes,
	amp_pool_t* pool);

AMP_DECLARE(void*) amp_pmemdup(
	const void* src,
	size_t bytes,
	amp_pool_t* pool);

AMP_DECLARE(char*) amp_pstrdup(
	const char* src,
	amp_pool_t* pool);

AMP_DECLARE(char*) amp_psprintf(
	amp_pool_t* pool,
	const char* format,
	...);

AMP_DECLARE(char*) amp_pvsprintf(
	amp_pool_t* pool,
	const char* format,
	va_list args);

AMP_DECLARE(char*) amp_pstrcat(
	amp_pool_t* pool,
	...);

AMP_C__END