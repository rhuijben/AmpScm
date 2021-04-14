#include "common.hpp" 
#include "amp_files.hpp"
#include "amp_buckets.hpp"

using namespace amp;

amp_error_t*
amp_file_open(
	amp_file_t** new_file,
	const char* filename,
	amp_file_open_t open_type,
	amp_file_share_t stare_type,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool)
{
	return amp_error_create(AMP_ERR_NOT_IMPLEMENTED, nullptr, nullptr);
}

amp_error_t *
amp_bucket_read(const char** data,
			size_t* data_len,
			amp_bucket_t* bucket,
			size_t requested,
			amp_pool_t* scratch_pool)
{
	amp_span span;
	auto bkt = static_cast<amp_bucket*>(bucket);

	auto r = bkt->read(
				&span,
				requested,
				scratch_pool);

	*data = span.data();
	*data_len = span.size_bytes();
	return amp_error_trace(r);
}