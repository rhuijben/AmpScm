#include <amp_buckets.hpp>
#include <amp_files.hpp>

using namespace amp;

static amp::amp_bucket::amp_bucket_type amp_aggregate_bucket_type("amp.aggregate");

amp_bucket_aggregate::amp_bucket_aggregate(bool keep_open, amp_allocator_t* allocator)
	: amp_bucket(&amp_aggregate_bucket_type, allocator)
{
	this->keep_open = keep_open;
	first = last = nullptr;
}

void
amp_bucket_aggregate::append(amp_bucket_t* bucket)
{
	bucket_list_t* item = amp_allocator_alloc<bucket_list_t>(allocator);

	if (!cur && !last)
		cur = item;

	item->next = nullptr;
	item->prev = last;
	item->bucket = bucket;

	if (last)
		last->next = item;
	else
		first = item;
	last = item;
}

void
amp_bucket_aggregate::prepend(amp_bucket_t* bucket)
{
	bucket_list_t* item = amp_allocator_alloc<bucket_list_t>(allocator);

	if (!cur)
	{
		append(bucket);
		return;
	}

	item->prev = cur->prev;
	item->next = cur;
	item->bucket = bucket;

	if (item->prev)
		item->prev->next = item;
	else
		first = item;

	cur->prev = item;
	cur = item;


	if (!cur && !last)
		cur = item;
}

amp_err_t*
amp_bucket_aggregate::read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	return nullptr;
}

amp_err_t*
amp_bucket_aggregate::read_until_eol(
	amp_span* data,
	amp_newline_t* found,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	return amp_bucket::read_until_eol(data, found, acceptable, requested, scratch_pool);
}

amp_err_t* 
amp_bucket_aggregate::read_bucket(
	amp_bucket_t** result,
	const amp_bucket_type_t* bucket_type,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);

	if (cur && (*cur->bucket)->get_bucket_type() == bucket_type)
	{
		*result = cur->bucket;

		if (cur->prev)
			cur->prev->next = cur->next;
		else
			first = cur->next;

		if (cur->next)
			cur->next->prev = cur->prev;
		else
			last = cur->prev;

		cur = cur->next ? cur->next : last;

		(*allocator)->free(cur);
		return AMP_NO_ERROR;
	}
	else
		*result = nullptr;
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_aggregate::peek(
	amp_span* data,
	bool no_poll,
	amp_pool_t* scratch_pool)
{
	return amp_bucket::peek(data, no_poll, scratch_pool);
}

amp_err_t*
amp_bucket_aggregate::read_skip(
	amp_off_t* skipped,
	amp_off_t requested,
	amp_pool_t* scratch_pool)
{
	return amp_bucket::read_skip(skipped, requested, scratch_pool);
}

amp_err_t*
amp_bucket_aggregate::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	return amp_bucket::read_remaining_bytes(remaining, scratch_pool);
}

amp_err_t*
amp_bucket_aggregate::reset(
	amp_pool_t* scratch_pool)
{
	return amp_bucket::reset(scratch_pool);
}

amp_err_t*
amp_bucket_aggregate::duplicate(amp_pool_t* scratch_pool)
{
	return amp_bucket::duplicate(scratch_pool);
}
