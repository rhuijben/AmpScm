#include <amp_buckets.hpp>
#include <amp_files.hpp>
using namespace amp;

static amp::amp_bucket::amp_bucket_type amp_simple_bucket_type("amp.simple");

amp_bucket_simple::amp_bucket_simple(const amp_bucket_type_t *type, amp_span span, amp_allocator_t* allocator)
	: amp_bucket(type, allocator)
{
	buffer = span;
	offset = 0;
}

amp_err_t*
amp_bucket_simple::read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	if (offset >= buffer.size())
		return amp_err_create(AMP_EOF, nullptr, nullptr);

	amp_span result = buffer.min_subspan(offset, requested);
	offset += result.size();
	*data = result;
	return AMP_NO_ERROR;
}

amp_err_t *
amp_bucket_simple::peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool)
{
	AMP_UNUSED(no_poll);
	AMP_UNUSED(scratch_pool);
	if (offset >= buffer.size())
		return amp_err_create(AMP_EOF, nullptr, nullptr);

	*data = buffer.subspan(offset);
	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket_simple::read_skip(
			amp_off_t* skipped,
			amp_off_t requested,
			amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	if (offset >= buffer.size())
		return amp_err_create(AMP_EOF, nullptr, nullptr);
	else if (requested <= (amp_off_t)buffer.size() - offset)
	{
		offset += (ptrdiff_t)requested;
		return AMP_NO_ERROR;
	}
	else
	{
		*skipped = (amp_off_t)buffer.size() - offset;
		offset = buffer.size();
		return AMP_NO_ERROR;
	}
}

amp_err_t*
amp_bucket_simple::read_remaining_bytes(
			amp_off_t* remaining,
			amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	*remaining = (buffer.size() - offset);
	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket_simple::reset(
			amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	offset = 0;
	return AMP_NO_ERROR;
}

amp_err_t* amp_bucket_simple::duplicate(amp_bucket_t** new_bucket, amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	*new_bucket = AMP_ALLOCATOR_NEW(amp_bucket_simple_copy, allocator, buffer.subspan(offset), allocator);

	return AMP_NO_ERROR;
}

amp_bucket_simple_copy::amp_bucket_simple_copy(amp_span span, amp_allocator_t* allocator)
	: amp_bucket_simple(&amp_simple_bucket_type, span, allocator)
{
	char* new_buffer = amp_allocator_alloc_n<char>(span.size(), allocator);
	memcpy(new_buffer, span.data(), span.size());

	buffer = amp_span(new_buffer, span.size());
}

void
amp_bucket_simple_copy::destroy(amp_pool_t* scratch_pool)
{
	(*allocator)->free(const_cast<char*>(buffer.data()));
	amp_bucket_simple::destroy(scratch_pool);
}

amp_bucket_simple_own::amp_bucket_simple_own(amp_span span, amp_allocator_t* allocator)
	: amp_bucket_simple(&amp_simple_bucket_type, span, allocator)
{
}
	
void 
amp_bucket_simple_own::destroy(amp_pool_t* scratch_pool)
{
	(*allocator)->free(const_cast<char*>(buffer.data()));
	amp_bucket_simple::destroy(scratch_pool);
}

amp_bucket_simple_const::amp_bucket_simple_const(amp_span span, amp_allocator_t* allocator)
	: amp_bucket_simple(&amp_simple_bucket_type, span, allocator)
{
}

void
amp_bucket_simple_const::destroy(amp_pool_t* scratch_pool)
{
	amp_bucket_simple::destroy(scratch_pool);
}
