#include "amp_types.hpp"
#include "amp_buckets.hpp"
#include "amp_files.hpp"

using namespace amp;

amp_err_t *
amp_bucket_read(const char** data,
				size_t* data_len,
				amp_bucket_t* bucket,
				size_t requested,
				amp_pool_t* scratch_pool)
{
	amp_span spandata;
	auto err = amp_err_trace((*bucket)->read(&spandata, requested, scratch_pool));

	*data = spandata.data();
	*data_len = spandata.size_bytes();
	return err;
}

amp_err_t*
amp_bucket_read_until_eol(
	const char** data,
	size_t* data_len,
	amp_newline_t* found,
	amp_bucket_t* bucket,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	amp_span spandata;
	auto err = amp_err_trace((*bucket)->read_until_eol(&spandata, found, acceptable, requested, scratch_pool));

	*data = spandata.data();
	*data_len = spandata.size_bytes();
	return err;
}


void
amp_bucket_destroy(
	amp_bucket_t* bucket,
	amp_pool_t* scratch_pool)
{
	static_cast<amp_bucket*>(bucket)->destroy(scratch_pool);
}


amp_err_t*
amp_bucket::read_until_eol(
	amp_span* data,
	amp_newline_t* found,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	amp_span peek_data;
	amp_err_t* err;
	bool single_cr_requested = false;

	AMP_ASSERT(acceptable && (acceptable == (acceptable & amp_newline_any)));

	err = peek(&peek_data, false, scratch_pool);
	if (AMP_IS_BUCKET_READ_ERROR(err))
		return amp_err_trace(err);
	else if (AMP_ERR_IS_EOF(err))
	{
		*data = amp_span();
		*found = amp_newline_none;
		return amp_err_trace(err);
	}

	amp_err_clear(err);

	if (peek_data.empty())
	{
		if ((acceptable & amp_newline_any) == amp_newline_crlf)
			requested = MIN(2, requested);
		else
			requested = MIN(1, requested);
	}
	else
	{
		// We have peek data
		ptrdiff_t rq_len = MIN(peek_data.size_bytes(), requested);

		const char* cr = (acceptable & (amp_newline_cr | amp_newline_crlf))
			? (const char*)memchr(peek_data.data(), '\r', rq_len)
			: nullptr;
		const char* lf = (acceptable & (amp_newline_lf | amp_newline_crlf))
			? (const char*)memchr(peek_data.data(), '\n', rq_len)
			: nullptr;

		cr = (cr && lf)
			? MIN(cr, lf)
			: (cr ? cr : lf);

		if (cr && *cr == '\r'
			&& (acceptable & amp_newline_crlf)
			&& (cr + 1 < peek_data.end() && cr[1] == '\n'))
		{
			requested = (cr + 2) - peek_data.data();
		}
		else if (cr)
		{
			requested = (cr + 1) - peek_data.data();

			if ((acceptable & amp_newline_crlf) && cr + 1 < peek_data.end())
				single_cr_requested = true;
		}
		else if ((acceptable & amp_newline_any) == amp_newline_crlf)
			requested = MIN(rq_len + 2, requested); // No newline in rq_len, and we need 2 chars for eol
		else
			requested = MIN(rq_len + 1, requested); // No newline in rq_len, and we need 1 char for eol
	}

	err = read(data, requested, scratch_pool);

	if (AMP_IS_BUCKET_READ_ERROR(err))
		return amp_err_trace(err);

	if (data->empty())
	{
		*found = amp_newline_none;
	}
	else if ((acceptable & amp_newline_crlf) && data->size_bytes() >= 2 &&
			 (*data)[data->size() - 1] == '\n' && (*data)[data->size() - 2] == '\r')
	{
		*found = amp_newline_crlf;
	}
	else if ((acceptable & amp_newline_lf) && (*data)[data->size() - 1] == '\n')
	{
		*found = amp_newline_lf;
	}
	else if (((acceptable & (amp_newline_crlf | amp_newline_cr)) == amp_newline_cr) && (*data)[data->size() - 1] == '\r')
	{
		*found = amp_newline_cr;
	}
	else if (acceptable & amp_newline_crlf && (*data)[data->size() - 1] == '\r')
	{
		if (single_cr_requested && requested == data->size())
			*found = amp_newline_cr;
		else
			*found = amp_newline_crlf_split;
	}
	else
		*found = amp_newline_none;

	return amp_err_trace(err);
}



amp_bucket_t*
amp_bucket_file_create(
	amp_file_t* file,
	amp_allocator_t* allocator,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);

	return AMP_ALLOCATOR_NEW(amp_bucket_file, allocator, file, allocator);
}
