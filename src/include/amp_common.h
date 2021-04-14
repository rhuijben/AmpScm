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

#define AMP_SUCCESS 0	

	typedef enum amp_file_open_t
	{
		amp_open_read = 1,//APR_FOPEN_READ,
		amp_open_write = 2,//APR_FOPEN_WRITE,
		amp_open_create = 4,//APR_FOPEN_CREATE,
		amp_open_append = 8,//APR_FOPEN_APPEND,
		amp_open_truncate = 16,//APR_FOPEN_TRUNCATE,

		amp_open_excl = 32,
		amp_open_buffered = 64,

		amp_open_no_cleanup = 128,
		amp_open_del_on_close = 256
	} amp_file_open_t;

	enum amp_file_share_t
	{
		amp_share_none = 0x00,
	};

	AMP_DECLARE(amp_error_t*)
		amp_cstring_to_utf8(
			const char** result,
			const char* input,
			amp_pool_t* result_pool);
	
#ifdef __cplusplus
}
#endif