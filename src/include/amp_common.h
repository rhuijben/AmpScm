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


	AMP_DECLARE(amp_err_t*)
		amp_cstring_to_utf8(
			const char** result,
			const char* input,
			amp_pool_t* result_pool);
	
#ifdef __cplusplus
}
#endif