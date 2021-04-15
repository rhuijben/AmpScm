#include "amp_types.h"

#pragma once

AMP_C__START

typedef enum amp_fopen_flags
{
	amp_fopen_read = 0x0001,
	amp_fopen_write = 0x0002,
	amp_fopen_create = 0x0004,
	// placeholder for append
	amp_fopen_truncate = 0x0010,

	amp_fopen_excl = 0x0020,

	amp_fopen_del_on_close = 0x01000
} amp_fopen_flags;

enum amp_fopen_share_flags
{
	amp_fopen_share_default
};

AMP_DECLARE(amp_err_t*)
amp_file_open(
	amp_file_t** file,
	const char* path,
	int flags,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool);

AMP_DECLARE(amp_err_t*)
amp_file_open_ex(
	amp_file_t** file,
	const char* path,
	int flags,
	int share_flags,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool);

AMP_DECLARE(amp_err_t*)
amp_file_read(
	size_t* bytes_read,
	amp_file_t* file,
	void* buffer,
	size_t buffer_size);

AMP_DECLARE(amp_err_t*)
amp_file_write(
	amp_file_t* file,
	const char* buffer,
	size_t bytes);

AMP_DECLARE(amp_err_t*)
amp_file_seek(
	amp_file_t* file,
	amp_off_t offset);

AMP_DECLARE(amp_err_t*)
amp_file_get_size(
	amp_off_t* size,
	amp_file_t* file);

AMP_DECLARE(amp_off_t)
amp_file_get_position(
	amp_file_t* file);

AMP_DECLARE(amp_err_t*)
amp_file_close(
	amp_file_t* file);

AMP_C__END
