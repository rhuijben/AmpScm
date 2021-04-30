#include "amp_buckets.hpp"
#include "amp_files.hpp"
#include "../amp_linkedlist.hpp"

using namespace amp;

static amp::amp_bucket::amp_bucket_type amp_aggregate_bucket_type("amp.aggregate");

amp_bucket_aggregate::amp_bucket_aggregate(bool keep_open, amp_allocator_t* allocator)
	: amp_bucket(&amp_aggregate_bucket_type, allocator)
{
	this->keep_open = keep_open;
	cur = first = last = nullptr;
}

void
amp_bucket_aggregate::append(amp_bucket_t* bucket)
{
	bucket_list_t* item = amp_allocator_alloc<bucket_list_t>(allocator);
	item->bucket = bucket;

	if (!cur && !last)
		cur = item;

	amp_linkedlist_append(first, last, item);
}

void
amp_bucket_aggregate::prepend(amp_bucket_t* bucket)
{
	bucket_list_t* item = amp_allocator_alloc<bucket_list_t>(allocator);
	item->bucket = bucket;

	if (!cur && !last)
		cur = item;

	amp_linkedlist_prepend(first, last, item);
}

void
amp_bucket_aggregate::cleanup(amp_pool_t *scratch_pool)
{
	if (keep_open)
		return;

	while (first && first != cur)
	{
		auto item = first;

		(*item->bucket)->destroy(scratch_pool);
		item->bucket = nullptr;

		amp_linkedlist_remove(first, last, item);

		(*allocator)->free(item);
	}
}

void
amp_bucket_aggregate::destroy(amp_pool_t *scratch_pool)
{
	keep_open = false;
	cur = nullptr;
	cleanup(scratch_pool);

	amp_bucket::destroy(scratch_pool);
}

amp_err_t*
amp_bucket_aggregate::read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	while (cur)
	{
		cleanup(scratch_pool);

		amp_err_t* err = (*cur->bucket)->read(data, requested, scratch_pool);

		if (AMP_ERR_IS_EOF(err))
		{
			amp_err_clear(err);
			cur = cur->next;
		}
		else if (err)
			return amp_err_trace(err);
		else if (data->empty())
		{
			AMP_ASSERT(data->size_bytes() >= 0); 
			// Bucket should have returned EOF or EAGAIN. Lets move on
			cur = cur->next;
		}
		else
			return AMP_NO_ERROR;
	}

	return amp_err_create(AMP_EOF, nullptr, nullptr);
}

amp_err_t*
amp_bucket_aggregate::read_until_eol(
	amp_span* data,
	amp_newline_t* found,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	while (cur)
	{
		cleanup(scratch_pool);

		amp_err_t* err = (*cur->bucket)->read_until_eol(data, found, acceptable, requested, scratch_pool);

		if (AMP_ERR_IS_EOF(err))
		{
			amp_err_clear(err);
			cur = cur->next;
		}
		else if (err)
			return amp_err_trace(err);
		else if (data->empty())
		{
			AMP_ASSERT(data->size_bytes() >= 0); 
			// Bucket should have returned EOF or EAGAIN. Lets move on
			cur = cur->next;
		}
		else
			return AMP_NO_ERROR;
	}

	return amp_err_create(AMP_EOF, nullptr, nullptr);
}

amp_err_t* 
amp_bucket_aggregate::read_bucket(
	amp_bucket_t** result,
	const amp_bucket_type_t* bucket_type,
	amp_pool_t* scratch_pool)
{
	cleanup(scratch_pool);

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
	while (cur)
	{
		cleanup(scratch_pool);

		amp_err_t* err = (*cur->bucket)->peek(data, no_poll, scratch_pool);

		if (AMP_ERR_IS_EOF(err))
		{
			amp_err_clear(err);
			cur = cur->next;
		}
		else if (err && !AMP_IS_BUCKET_READ_ERROR(err))
		{
			amp_err_clear(err);
			return AMP_NO_ERROR;
		}
		else
			return amp_err_trace(err);
	}

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_aggregate::read_skip(
	amp_off_t* skipped,
	amp_off_t requested,
	amp_pool_t* scratch_pool)
{
	*skipped = 0;
	while (cur && requested)
	{
		cleanup(scratch_pool);

		amp_off_t this_skip;

		amp_err_t* err = (*cur->bucket)->read_skip(&this_skip, requested, scratch_pool);

		if (!err)
		{
			*skipped += this_skip;
			requested -= this_skip;

			if (!requested)
				return AMP_NO_ERROR;
		}

		if (AMP_ERR_IS_EOF(err))
		{
			amp_err_clear(err);
			cur = cur->next;
		}
		else if (err)
			return amp_err_trace(err);
		else if (this_skip <= 0)
		{
			AMP_ASSERT(this_skip >= 0); // Bucket should have returned EOF or EAGAIN. Lets move on
			cur = cur->next;
		}
		else
			return AMP_NO_ERROR;
	}

	return amp_err_create(AMP_EOF, nullptr, nullptr);
}

amp_err_t*
amp_bucket_aggregate::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	*remaining = 0;

	auto t = cur;
	while (t)
	{
		amp_off_t this_remaining;
		amp_err_t *err = (*cur->bucket)->read_remaining_bytes(&this_remaining, scratch_pool);

		if (err)
			*remaining = -1;
			
		AMP_ERR(err);

		if (this_remaining >= 0)
		{
			*remaining += this_remaining;
			t = t->next;
		}
		else
		{
			*remaining = -1;
			return amp_err_create(AMP_ERR_NOT_SUPPORTED, nullptr, nullptr);
		}
	}

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_aggregate::reset(
	amp_pool_t* scratch_pool)
{
	if (!keep_open)
		return amp_err_create(AMP_ERR_NOT_SUPPORTED, nullptr, nullptr);

	auto t = first;
	while (t)
	{
		amp_err_t* err = (*t->bucket)->reset(scratch_pool);

		if (err)
			return amp_err_trace(err);

		if (t == cur)
		{
			cur = first;
			return AMP_NO_ERROR;
		}
		t = t->next;
	}

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_aggregate::duplicate(
	amp_bucket_t **result,
	bool for_reset,
	amp_pool_t* scratch_pool)
{
	amp_bucket_aggregate* bk = AMP_ALLOCATOR_NEW(amp_bucket_aggregate, allocator, keep_open, allocator);

	// No duplicate. We promise to keep the data here

	if (cur)
	{
		amp_bucket_t* new_cur;
		amp_err_t* err = (*cur->bucket)->duplicate(&new_cur, for_reset, scratch_pool);

		if (err)
		{
			amp_bucket_destroy(bk, scratch_pool);
			return amp_err_trace(err);
		}
		bk->append(new_cur);
	}

	auto t = cur ? cur->prev : last;
	while (t)
	{
		amp_bucket_t* new_prev;
		amp_err_t* err = (*t->bucket)->duplicate(&new_prev, for_reset, scratch_pool);

		if (err)
		{
			amp_bucket_destroy(bk, scratch_pool);
			return amp_err_trace(err);
		}
		bk->prepend(new_prev);

		t = t->prev;
	}

	t = cur ? cur->next : nullptr;
	while (t)
	{
		amp_bucket_t* new_prev;
		amp_err_t* err = (*t->bucket)->duplicate(&new_prev, for_reset, scratch_pool);

		if (err)
		{
			amp_bucket_destroy(bk, scratch_pool);
			return amp_err_trace(err);
		}
		bk->append(new_prev);

		t = t->next;
	}

	*result = bk;
	return AMP_NO_ERROR;
}


amp_bucket_t *
amp_bucket_aggregate_create(amp_allocator_t* alloc)
{
	return AMP_ALLOCATOR_NEW(amp_bucket_aggregate, alloc, false, alloc);
}

amp_bucket_t *
amp_bucket_simple_create(const char* data, ptrdiff_t size, amp_allocator_t* alloc)
{
	return AMP_ALLOCATOR_NEW(amp_bucket_simple_const, alloc, amp_span(data, size), alloc);
}

amp_bucket_t *
amp_bucket_simple_own_create(const char* data, ptrdiff_t size, amp_allocator_t* alloc)
{
	return AMP_ALLOCATOR_NEW(amp_bucket_simple_own, alloc, amp_span(data, size), alloc);
}

amp_bucket_t *
amp_bucket_simple_copy_create(const char* data, ptrdiff_t size, amp_allocator_t* alloc)
{
	return AMP_ALLOCATOR_NEW(amp_bucket_simple_copy, alloc, amp_span(data, size), alloc);
}

void
amp_bucket_aggregate_append(
	amp_bucket_t* aggregate,
	amp_bucket_t* to_append)
{
	AMP_ASSERT((*aggregate)->get_bucket_type() == &amp_aggregate_bucket_type);

	static_cast<amp_bucket_aggregate*>(aggregate)->append(to_append);
}

void
amp_bucket_aggregate_prepend(
	amp_bucket_t* aggregate,
	amp_bucket_t* to_prepend)
{
	AMP_ASSERT((*aggregate)->get_bucket_type() == &amp_aggregate_bucket_type);

	static_cast<amp_bucket_aggregate*>(aggregate)->prepend(to_prepend);
}
