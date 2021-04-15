#include "common.hpp" 
#include "amp_files.hpp"
#include "amp_buckets.hpp"

using namespace amp;

amp_err_t *
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
	return amp_err_trace(r);
}