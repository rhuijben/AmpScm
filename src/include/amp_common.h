/**
 * @copyright
 * ===========================================================================
 * Licensed to the AmpScm project under one or more contributor license
 * agreements. See the LICENSE file distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This file is licensed to you under the Apache License, Version 2.0 ("the
 * License"). You may not use this file except in compliance with the License.
 * You may obtain a copy of the license at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.  See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */

#pragma once

#include "amp_types.h"

#ifdef __cplusplus
extern "C" {
#endif

	typedef struct amp_error_t amp_error_t;
	typedef struct amp_file_t amp_file_t;
	typedef apr_pool_t amp_pool_t;
	typedef off_t amp_off_t;

	typedef char amp_boolean_t;

#define AMP_SUCCESS 0

	typedef enum amp_newline_t
	{
		amp_newline_nl = 0x01,
		amp_newline_cr = 0x02,
		amp_newline_crlf = 0x04,

		amp_newline_any = amp_newline_nl | amp_newline_cr | amp_newline_crlf
	} amp_newline_t;

	typedef enum amp_file_open_t
	{
		amp_open_read = APR_FOPEN_READ,
		amp_open_write = APR_FOPEN_WRITE,
		amp_open_create = APR_FOPEN_CREATE,
		amp_open_append = APR_FOPEN_APPEND,
		amp_open_truncate = APR_FOPEN_TRUNCATE,

		amp_open_excl = APR_FOPEN_EXCL,
		amp_open_buffered = APR_FOPEN_BUFFERED,

		amp_open_no_cleanup = APR_FOPEN_NOCLEANUP,
		amp_open_del_on_close = APR_FOPEN_DELONCLOSE
	} amp_file_open_t;

	enum amp_file_share_t
	{
		amp_share_none = 0x00,
	};

	AMP_DLL(amp_error_t*)
		amp_file_open(
			amp_file_t** new_file,
			const char* filename,
			amp_file_open_t open_type,
			amp_file_share_t stare_type,
			amp_pool_t* result_pool,
			amp_pool_t* scratch_pool
		);

	AMP_DLL(amp_error_t*)
		amp_cstring_to_utf8(
			const char** result,
			const char* input,
			amp_pool_t* result_pool);

	AMP_DLL(amp_error_t*)
		amp_cmdline_printf(amp_pool_t* scratch_pool, const char* fmt, ...);

	AMP_DLL(amp_error_t*)
		amp_cmdline_puts(const char* text, amp_pool_t *scratch_pool);
	
#ifdef __cplusplus
}
#endif