#include "amp_types.hpp"
#include "amp_buckets.hpp"
#include "amp_files.hpp"

using namespace amp;

amp_err_t*
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

	AMP_ASSERT(acceptable && (acceptable == (acceptable & amp_newline_any_split)));

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
		if ((acceptable & amp_newline_any_split) == amp_newline_crlf)
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
		const char* lf = (acceptable & (amp_newline_lf))
			? (const char*)memchr(peek_data.data(), '\n', rq_len)
			: nullptr;
		const char* zero = (acceptable & amp_newline_0)
			? (const char*)memchr(peek_data.data(), '\0', rq_len)
			: nullptr;

		// Fold zero in lf
		lf = (lf && zero) ? MIN(lf, zero) : (lf ? lf : zero);

		if (cr && (acceptable & (amp_newline_cr | amp_newline_crlf)) == amp_newline_crlf)
		{
			const char* rq_end = peek_data.data() + rq_len;

			// If we have a cr but not a cr+lf, we want to check the next cr (if any)
			while (cr && (!lf || cr < lf) && (&cr[1] < rq_end) && cr[1] != '\n')
			{
				cr = (const char*)memchr(cr + 1, '\r', rq_end - cr - 1);
			}
		}

		// fold lf (and zero) in cr
		cr = (cr && lf)
			? MIN(cr, lf)
			: (cr ? cr : lf);

		ptrdiff_t linelen = cr - peek_data.data();

		if (cr
			&& *cr == '\r'
			&& (acceptable & amp_newline_crlf)
			&& (linelen + 1 < rq_len))
		{
			if (cr[1] == '\n')
				requested = linelen + 2; // cr+lf
			else if (acceptable & amp_newline_cr)
			{
				requested = linelen + 1; // cr without lf
				single_cr_requested = true;
			}
			else
			{
				// easy out. Just include the single character after the cr
				requested = linelen + 2; // cr+lf
			}
		}
		else if (cr)
		{
			requested = linelen + 1;
		}
		else if ((acceptable & amp_newline_any_split) == amp_newline_crlf)
			requested = MIN(rq_len + 2, requested); // No newline in rq_len, and we need 2 chars for eol
		else
			requested = MIN(rq_len + 1, requested); // No newline in rq_len, and we need 1 char for eol
	}

	err = read(data, requested, scratch_pool);

	if (AMP_IS_BUCKET_READ_ERROR(err))
		return amp_err_trace(err);

	if (found)
	{
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
		else if (acceptable & amp_newline_0 && (*data)[data->size() - 1] == '\0')
			*found = amp_newline_0;
		else
			*found = amp_newline_none;
	}

	return amp_err_trace(err);
}

amp_err_t* 
amp_bucket::read_bucket(
	amp_bucket_t** result,
	const amp_bucket_type_t* bucket_type,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	AMP_UNUSED(bucket_type);

	*result = nullptr;
	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket::peek(
	amp_span* data,
	bool no_poll,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	AMP_UNUSED(no_poll);

	*data = amp_span("", 0);
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket::read_skip(
	amp_off_t* skipped,
	amp_off_t requested,
	amp_pool_t* scratch_pool)
{
	amp_span result;
	*skipped = 0;

	while (requested > 0)
	{
		ptrdiff_t rq = (requested > INTPTR_MAX) ? INTPTR_MAX : (ptrdiff_t)requested;

		AMP_ERR(read(&result, rq, scratch_pool));

		if (!result.size_bytes())
		{
			if (*skipped)
				break;
			else
				return amp_err_create(AMP_EOF, nullptr, nullptr);
		}

		*skipped += result.size_bytes();
		requested -= rq;
	}
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	*remaining = -1;

	return amp_err_create(AMP_ERR_NOT_SUPPORTED, nullptr, nullptr);
}

amp_err_t*
amp_bucket::reset(amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);

	return amp_err_create(AMP_ERR_NOT_IMPLEMENTED, nullptr, nullptr);
}

amp_err_t*
amp_bucket::duplicate(
	amp_bucket_t** result,
	bool for_reset,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(for_reset);
	AMP_UNUSED(scratch_pool);

	*result = nullptr;

	return amp_err_create(AMP_ERR_NOT_IMPLEMENTED, nullptr, nullptr);
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
