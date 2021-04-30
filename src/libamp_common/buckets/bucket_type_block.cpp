#include "amp_buckets.hpp"
#include "amp_files.hpp"

using namespace amp;

static amp::amp_bucket::amp_bucket_type amp_block_bucket_type("amp.block");

amp_bucket_block::amp_bucket_block(amp_bucket_t *wrapped_bucket, amp_allocator_t* allocator)
	: amp_bucket(&amp_block_bucket_type, allocator)
{
	wrapped = wrapped_bucket;
}

// No destroy as we explicitly disown the bucket

amp_err_t* 
amp_bucket_block::read(
	amp_span* data,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->read(data, requested, scratch_pool));
}

amp_err_t* 
amp_bucket_block::read_until_eol(
	amp_span* data,
	amp_newline_t* found,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->read_until_eol(data, found, acceptable, requested, scratch_pool));
}

amp_err_t* 
amp_bucket_block::read_bucket(
	amp_bucket_t** result,
	const amp_bucket_type_t* bucket_type,
	amp_pool_t* scratch_pool)
{
	// We don't want to read inner buckets... as that would break the blocking
	return amp_err_trace(amp_bucket::read_bucket(result, bucket_type, scratch_pool));
}

amp_err_t* 
amp_bucket_block::peek(
	amp_span* data,
	bool no_poll,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->peek(data, no_poll, scratch_pool));
}

amp_err_t* 
amp_bucket_block::read_skip(
	amp_off_t* skipped,
	amp_off_t requested,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->read_skip(skipped, requested, scratch_pool));
}

amp_err_t* 
amp_bucket_block::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->read_remaining_bytes(remaining, scratch_pool));
}

amp_err_t* 
amp_bucket_block::reset(amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->reset(scratch_pool));
}

amp_err_t* 
amp_bucket_block::duplicate(
	amp_bucket_t** result,
	bool for_reset,
	amp_pool_t* scratch_pool)
{
	// Yes the duplicate *IS* owned, or it would just leak... Other option would be an error
	return (*wrapped)->duplicate(result, for_reset, scratch_pool);
}

amp_off_t
amp_bucket_block::get_position()
{
	return (*wrapped)->get_position();
}


amp_bucket_t*
amp_bucket_block_create(amp_bucket_t* wrapped,
						amp_allocator_t* allocator)
{
	return AMP_ALLOCATOR_NEW(amp_bucket_block, allocator, wrapped, allocator);
}
