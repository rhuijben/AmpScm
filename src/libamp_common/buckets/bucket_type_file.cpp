#include <amp_buckets.hpp>
#include <amp_files.hpp>
using namespace amp;

static amp::amp_bucket::amp_bucket_type amp_file_bucket_type("amp.file");

static const int BUFFER_SIZE = 65536;
static const int BUFFER_MIN_ALIGN = 4096;

amp_bucket_file::amp_bucket_file(amp_file_t* file, amp_allocator_t* allocator)
	:amp_bucket(&amp_file_bucket_type, allocator)
{
	AMP_ASSERT(file);

	this->file = file;

	amp_off_t size;
	amp_err_t* err = amp_file_get_size(&size, file);

	if (err)
	{
		amp_err_clear(err);
		file_remaining = -1;
	}
	else
	{
		amp_off_t pos = amp_file_get_position(file);

		if (pos >= 0 && pos <= size)
			file_remaining = size - pos;
		else
			file_remaining = -1;
	}
	available = 0;
	position = 0;

	buffer = span<char>(amp_allocator_alloc_n<char>(BUFFER_SIZE, allocator), BUFFER_SIZE);
}

void amp_bucket_file::destroy(amp_pool_t* pool)
{
	amp_allocator_free(buffer.data(), allocator);

	amp_bucket::destroy(pool);
}

amp_err_t*
amp_bucket_file::refill(ptrdiff_t requested)
{
	if (available > 4096 || available > requested)
		return AMP_NO_ERROR; // Nothing to refill, or we must move more data than we want


	// Modern disks and OS caches generally work in 4KB or bigger blocks. Let's keep things nicely
	// aligned. Typically on 64 KByte, but on 4K when using eol read refills
	// Note that get_current_position() is just a 

	ptrdiff_t fixup = ((*file)->get_current_position() & (BUFFER_MIN_ALIGN - 1));

	if (fixup)
		fixup = (BUFFER_MIN_ALIGN - fixup);

	if (available > 0)
	{		
		{	
			// Move data to the first 4KB
			ptrdiff_t new_pos = position & (BUFFER_MIN_ALIGN - 1) + fixup;

			AMP_ASSERT(new_pos + available < position); // Regions can't overlap with memcpy

			memcpy(&buffer[new_pos], &buffer[position], available);
			position = new_pos;
		}

		ptrdiff_t extra_available;

		// And now fill up the remaining buffer space
		AMP_ERR((*file)->read(&extra_available, buffer.subspan(position + available)));

		available += extra_available;
	}
	else
	{
		position = fixup;
		AMP_ERR((*file)->read(&available, buffer.subspan(fixup)));
	}

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_file::read(amp_span* data,
					  ptrdiff_t requested,
					  amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	AMP_ASSERT(requested >= 0);

	if (!available)
		AMP_ERR(refill(requested));

	if (requested > available)
		requested = available;

	*data = buffer.subspan(position, requested);
	position += requested;
	available -= requested;

	if (file_remaining > 0)
		file_remaining -= requested;

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_file::read_until_eol(
	amp_span* data,
	amp_newline_t* found,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	AMP_ASSERT(requested > 0);

	// For newline scanning the usual peek method may be painfull. Lets handle this smart
	auto err = refill(requested);

	if (err && !AMP_IS_BUCKET_READ_ERROR(err))
		amp_err_clear(err);
	else
		AMP_ERR(err);

	return amp_err_trace(amp_bucket::read_until_eol(data, found, acceptable, requested, scratch_pool));
}

amp_err_t*
amp_bucket_file::peek(
	amp_span* data,
	bool no_poll,
	amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	if (!available && !no_poll)
	{
		amp_err_t* err = refill(AMP_READ_ALL_AVAIL);

		if (err && !AMP_IS_BUCKET_READ_ERROR(err))
			amp_err_clear(err);
		else
			AMP_ERR(err);
	}

	*data = amp_span(&buffer[position], available);
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_file::reset(amp_pool_t* scratch_pool)
{
	AMP_UNUSED(scratch_pool);
	amp_err_t* err = (*file)->seek(0);
	if (!err)
	{
		if (file_remaining > 0)
			file_remaining += position;

		available = 0;
		position = 0;
	}
	return amp_err_trace(err);
}

amp_err_t*
amp_bucket_file::read_skip(amp_off_t* skipped,
						   amp_off_t requested,
						   amp_pool_t* scratch_pool)
{
	AMP_ASSERT(requested > 0);

	if (available)
	{
		ptrdiff_t skip = (requested < available) ? available : (ptrdiff_t)requested;

		available -= skip;
		position += skip;

		if (file_remaining > 0)
			file_remaining -= skip;

		requested -= skip;
		*skipped = skip;

		return AMP_NO_ERROR;
	}

	amp_off_t fsz;
	amp_err_t* err = (*file)->get_current_size(&fsz);

	if (err)
	{
		amp_err_clear(err);
		return amp_err_trace(amp_bucket::read_skip(skipped, requested, scratch_pool));
	}

	amp_off_t cur_pos = (*file)->get_current_position();
	amp_off_t left = (fsz - cur_pos);

	if (left <= 0)
	{
		*skipped = 0;
		return amp_err_create(AMP_EOF, nullptr, nullptr);
	}

	if (requested > left)
		requested = left;

	AMP_ERR((*file)->seek(cur_pos + requested));

	*skipped = requested;

	if (file_remaining > 0)
		file_remaining -= requested;

	return AMP_NO_ERROR;
}

amp_err_t* 
amp_bucket_file::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	if (file_remaining >= 0)
	{
		*remaining = file_remaining;
		return AMP_NO_ERROR;
	}

	return amp_bucket::read_remaining_bytes(remaining, scratch_pool);
}
