#ifdef _WIN32
#include <Windows.h>
#include <bcrypt.h>
#else
// TODO: Add OPENSSL variant
#endif
#include <zlib.h>

#include "amp_buckets.hpp"

using namespace amp;

static amp::amp_bucket::amp_bucket_type limit_bucket_type("amp.limit");


amp_bucket_limit::amp_bucket_limit(
	amp_bucket_t* wrap_bucket,
	amp_off_t limit,
	amp_allocator_t* allocator)
	: amp_bucket(&limit_bucket_type, allocator)
{
	AMP_ASSERT(wrap_bucket && limit >= 0);
	wrapped = wrap_bucket;

	end_offset = remaining = limit;
	buf_position = true;
}

void 
amp_bucket_limit::destroy(amp_pool_t* scratch_pool)
{
	if (wrapped)
		(*wrapped)->destroy(scratch_pool);

	amp_bucket::destroy(scratch_pool);
}

amp_err_t* 
amp_bucket_limit::read(
		amp_span* data,
		ptrdiff_t requested,
		amp_pool_t* scratch_pool)
{
	if (!remaining)
	{
		if (wrapped)
		{
			(*wrapped)->destroy(scratch_pool);
			wrapped = nullptr;
		}

		return amp_err_create(AMP_EOF, nullptr, nullptr);
	}
	else if (requested > remaining)
		requested = (ptrdiff_t)remaining;

	AMP_ERR((*wrapped)->read(data, requested, scratch_pool));

	buf_position += data->size_bytes();
	remaining -= data->size_bytes();

	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket_limit::read_until_eol(
		amp_span* data,
		amp_newline_t* found,
		amp_newline_t acceptable,
		ptrdiff_t requested,
		amp_pool_t* scratch_pool)
{
	if (!remaining)
	{
		if (wrapped)
		{
			(*wrapped)->destroy(scratch_pool);
			wrapped = nullptr;
		}

		return amp_err_create(AMP_EOF, nullptr, nullptr);
	}
	else if (requested > remaining)
		requested = (ptrdiff_t)remaining;

	AMP_ERR((*wrapped)->read_until_eol(data, found, acceptable, requested, scratch_pool));

	buf_position += data->size_bytes();
	remaining -= data->size_bytes();

	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket_limit::peek(
		amp_span* data,
		bool no_poll,
		amp_pool_t* scratch_pool)
{
	if (!wrapped)
		return amp_err_create(AMP_EOF, nullptr, nullptr);

	AMP_ERR((*wrapped)->peek(data, no_poll, scratch_pool));

	if (data->size_bytes() > remaining)
		*data = data->subspan(0, (ptrdiff_t)remaining);
	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket_limit::reset(
		amp_pool_t * scratch_pool)
{
	if (!wrapped)
		return amp_err_create(AMP_ERR_NOT_SUPPORTED, nullptr, nullptr);

	AMP_ERR((*wrapped)->reset(scratch_pool));

	buf_position = 0;
	remaining = end_offset;
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_limit::read_remaining_bytes(
			amp_off_t* remaining,
			amp_pool_t* scratch_pool)
{
	*remaining = this->remaining;
	return AMP_NO_ERROR;
}

amp_off_t
amp_bucket_limit::get_position()
{
	return buf_position;
}

amp_err_t* 
amp_bucket_limit::duplicate(
		amp_bucket_t** result,
		bool for_reset,
		amp_pool_t* scratch_pool)
{
	amp_bucket_t* wr;

	if (!wrapped)
		return amp_err_create(AMP_ERR_NOT_SUPPORTED, nullptr, nullptr);

	AMP_ERR((*wrapped)->duplicate(&wr, for_reset, scratch_pool));

	amp_bucket_limit* l = AMP_ALLOCATOR_NEW(amp_bucket_limit, allocator, wr, end_offset, allocator);

	l->buf_position = buf_position;
	l->remaining = remaining;
	*result = l;

	return AMP_NO_ERROR;
}
