#include <amp_buckets.hpp>
#include <amp_files.hpp>
#include <zlib.h>

#ifdef _MSC_VER
#ifdef _DEBUG
#pragma comment(lib, "zlibd.lib")
#else
#pragma comment(lib, "zlib.lib")
#endif
#endif

using namespace amp;

static amp::amp_bucket::amp_bucket_type compress_bucket_type("amp.compress");
static amp::amp_bucket::amp_bucket_type decompress_bucket_type("amp.decompress");


amp_err_t*
amp_bucket_compress_create(
	amp_bucket_t** new_bucket,
	amp_bucket_t* to_compress,
	amp_compression_algorithm_t algorithm,
	int level,
	ptrdiff_t buffer_size,
	amp_allocator_t* allocator)
{
	*new_bucket = *new_bucket = AMP_ALLOCATOR_NEW(amp_bucket_compress, allocator, to_compress, algorithm, buffer_size, level, allocator);
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_decompress_create(
	amp_bucket_t** new_bucket,
	amp_bucket_t* to_compress,
	amp_compression_algorithm_t algorithm,
	ptrdiff_t buffer_size,
	amp_allocator_t* allocator)
{
	*new_bucket = AMP_ALLOCATOR_NEW(amp_bucket_decompress, allocator, to_compress, algorithm, buffer_size, allocator);

	return AMP_NO_ERROR;
}

amp_bucket_compression::amp_bucket_compression(const amp_bucket_type_t* bucket_type, amp_bucket_t* wrapped_bucket, ptrdiff_t buffer_size, amp_allocator_t* allocator)
	: amp_bucket(bucket_type, allocator)
{
	AMP_ASSERT(wrapped_bucket
			   && (buffer_size < 0 || buffer_size >= 512));

	wrapped = wrapped_bucket;
	read_eof = false;
	algorithm = amp_compression_algorithm_none;
	// read_buffer
	read_position = 0;

	size_t sz = (buffer_size < 0) ? 8192 : buffer_size;

	write_buffer = span<char>(amp_allocator_alloc_n<char>(sz, allocator), sz);
	write_position = 0;
	write_read_position = 0;
	p0 = nullptr;
	buf_position = 0;
}

void
amp_bucket_compression::destroy(amp_pool_t* scratch_pool)
{
	done();
	(*wrapped)->destroy(scratch_pool);
	(*allocator)->free(write_buffer.data());

	if (p0)
		(*allocator)->free(p0);

	amp_bucket::destroy(scratch_pool);
}

static void* zlib_alloc(void* opaque, uInt items, uInt size)
{
	auto alloc = reinterpret_cast<amp_allocator_t*>(opaque);

	return (*alloc)->alloc(items * size);
}

static void zlib_free(void* opaque, void* address)
{
	auto alloc = reinterpret_cast<amp_allocator_t*>(opaque);

	return (*alloc)->free(address);
}

#define ALGORITHM_ZLIB(alg)							\
	((alg) == amp_compression_algorithm_zlib		\
      || (alg) == amp_compression_algorithm_deflate \
      || (alg) == amp_compression_algorithm_gzip)

amp_bucket_decompress::amp_bucket_decompress(
	amp_bucket_t* wrapped_bucket,
	amp_compression_algorithm_t compression_algorithm,
	ptrdiff_t bufsize,
	amp_allocator_t* allocator)
	: amp_bucket_compression(&decompress_bucket_type, wrapped_bucket, bufsize, allocator)
{
	AMP_ASSERT(ALGORITHM_ZLIB(compression_algorithm) || compression_algorithm == amp_compression_algorithm_none);

	algorithm = compression_algorithm;
}

void
amp_bucket_decompress::done()
{
	if (ALGORITHM_ZLIB(algorithm))
	{
		z_stream* zs = reinterpret_cast<z_stream*>(p0);

		if (p0)
			inflateEnd(zs);
	}
	else
	{

	}
}


amp_err_t*
amp_bucket_decompress::init()
{
	if (ALGORITHM_ZLIB(algorithm))
	{
		z_stream* zs = amp_allocator_alloc<z_stream>(allocator);
		p0 = zs;
		memset(zs, 0, sizeof(*zs));

		zs->zalloc = zlib_alloc;
		zs->zfree = zlib_free;
		zs->opaque = allocator;

		int windowBits = 15;
		switch (algorithm)
		{
			case amp_compression_algorithm_zlib:
				break;
			case amp_compression_algorithm_deflate:
				windowBits = -15; // No headers
				break;
			case amp_compression_algorithm_gzip:
				windowBits += 16; // gzip headers
				break;
		}

		int r = inflateInit2(zs, windowBits);
		if (r != Z_OK)
		{
			if (zs->msg)
				return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib initialization failed: %s", zs->msg);
			else
				return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib initialization failed with code %d", r);
		}
	}

	AMP_ASSERT(p0 != nullptr);
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_decompress::refill(ptrdiff_t requested, amp_pool_t* scratch_pool)
{
	if (!this->p0 && algorithm != amp_compression_algorithm_none)
		AMP_ERR(init());

	bool retry_refill;
	do
	{
		retry_refill = false;
		bool did_peek = false;

		if (!read_eof && read_position >= read_buffer.size_bytes())
		{
			read_position = 0;

			amp_err_t* err = (*wrapped)->peek(&read_buffer, false, scratch_pool);

			if (err || read_buffer.size_bytes() == 0)
			{
				amp_err_clear(err);

				err = (*wrapped)->read(&read_buffer, 1, scratch_pool);

				if (err)
				{
					read_buffer = amp_span();

					if (!AMP_ERR_IS_EOF(err))
						return amp_err_trace(err);

					amp_err_clear(err);
					read_eof = true;
				}
			}
			else
			{
				did_peek = true;
			}
		}

		if (write_position == write_read_position)
		{
			write_position = 0;
			write_read_position = 0;
		}

		amp_span data_in = read_buffer.subspan(read_position);
		amp::span<char> data_out = write_buffer.min_subspan(write_position, requested); // Just fill buffer instead?

		if (ALGORITHM_ZLIB(algorithm))
		{
			z_stream* zs = reinterpret_cast<z_stream*>(p0);

			zs->next_in = (Bytef*)const_cast<char*>(data_in.data());
			zs->avail_in = data_in.size_bytes();

			zs->next_out = (Bytef*)data_out.data();
			zs->avail_out = data_out.size_bytes();

			int r = inflate(zs, read_eof ? Z_FINISH : Z_SYNC_FLUSH); // Write as much inflated data as possible

			read_position += (const char*)zs->next_in - data_in.data();
			write_position += (const char*)zs->next_out - data_out.data();

			if (r == Z_STREAM_END)
			{
				read_eof = true;
			}
			else if (r != Z_OK)
			{
				if (zs->msg)
					return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib inflate failed: %s", zs->msg);
				else
					return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib inflate failed with code %d", r);
			}

			if (write_position == 0)
				retry_refill = true;
		}
		else // Algorithm none
		{
			ptrdiff_t szcopy = MIN(read_buffer.size(), write_buffer.size());
			memcpy(data_out.data(), data_in.data(), szcopy);
			read_position += szcopy;
			write_position += szcopy;
		}

		if (did_peek)
		{
			// We only peeked the data, and performed no actual read. Let's perform the requested read now
			amp_err_t *err = amp_err_trace((*wrapped)->read(&read_buffer, read_position, scratch_pool));

			if (AMP_IS_BUCKET_READ_ERROR(err))
				return err;
			else if (err)
				amp_err_clear(err);

			AMP_ASSERT(read_buffer.size_bytes() == read_position); // We are at the end of the read buffer.
		}
	} while (retry_refill && !read_eof);

	return AMP_NO_ERROR;
}

amp_bucket_compress::amp_bucket_compress(
	amp_bucket_t* wrapped_bucket,
	amp_compression_algorithm_t compression_algorithm,
	ptrdiff_t bufsize,
	int level,
	amp_allocator_t* allocator)
	: amp_bucket_compression(&compress_bucket_type, wrapped_bucket, bufsize, allocator)
{
	AMP_ASSERT(ALGORITHM_ZLIB(compression_algorithm) || algorithm == amp_compression_algorithm_none);

	algorithm = compression_algorithm;
	compression_level = level;
}

void
amp_bucket_compress::done()
{
	if (ALGORITHM_ZLIB(algorithm))
	{
		z_stream* zs = reinterpret_cast<z_stream*>(p0);

		if (p0)
			deflateEnd(zs);
	}
	else
	{

	}
}


amp_err_t*
amp_bucket_compress::init()
{
	if (ALGORITHM_ZLIB(algorithm))
	{
		z_stream* zs = amp_allocator_alloc<z_stream>(allocator);
		p0 = zs;
		memset(zs, 0, sizeof(*zs));

		zs->zalloc = zlib_alloc;
		zs->zfree = zlib_free;
		zs->opaque = allocator;

		int windowBits = 15;
		switch (algorithm)
		{
			case amp_compression_algorithm_zlib:
				break;
			case amp_compression_algorithm_deflate:
				windowBits = -15; // No headers
				break;
			case amp_compression_algorithm_gzip:
				windowBits += 16; // gzip headers
				break;
		}

		int r = deflateInit2(zs,
							compression_level < 0 ? -1 : compression_level,
							Z_DEFLATED,
							windowBits,
							8 /* default memory usage (1 = min ram, 9 = max speed) */,
							Z_DEFAULT_STRATEGY);
		if (r != Z_OK)
		{
			if (zs->msg)
				return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib initialization failed: %s", zs->msg);
			else
				return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib initialization failed with code %d", r);
		}
	}

	AMP_ASSERT(p0 != nullptr || algorithm == amp_compression_algorithm_none);
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_compress::refill(ptrdiff_t requested, amp_pool_t* scratch_pool)
{
	if (!this->p0 && algorithm != amp_compression_algorithm_none)
		AMP_ERR(init());

	bool retry_refill;

	do
	{
		retry_refill = false;

		if (!read_eof && read_position >= read_buffer.size_bytes())
		{
			read_position = 0;
			auto err = (*wrapped)->read(&read_buffer, requested, scratch_pool);
			if (err)
			{
				read_buffer = amp_span();

				if (!AMP_ERR_IS_EOF(err))
					return amp_err_trace(err);

				amp_err_clear(err);
				read_eof = true;
			}
		}

		if (write_position == write_read_position)
		{
			write_position = 0;
			write_read_position = 0;
		}

		amp_span data_in = read_buffer.subspan(read_position);
		amp::span<char> data_out = write_buffer.min_subspan(write_position, requested); // Just fill buffer instead?

		if (ALGORITHM_ZLIB(algorithm))
		{
			z_stream* zs = reinterpret_cast<z_stream*>(p0);

			zs->next_in = (Bytef*)const_cast<char*>(data_in.data());
			zs->avail_in = data_in.size_bytes();

			zs->next_out = (Bytef*)data_out.data();
			zs->avail_out = data_out.size_bytes();

			int r = deflate(zs, read_eof ? Z_FINISH : Z_NO_FLUSH); // No forced blocks until done

			read_position += (const char*)zs->next_in - data_in.data();
			write_position += (const char*)zs->next_out - data_out.data();

			if (r == Z_STREAM_END)
			{
				read_eof = true;
			}
			else if (r != Z_OK)
			{
				if (zs->msg)
					return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib inflate failed: %s", zs->msg);
				else
					return amp_err_createf(AMP_EGENERAL, nullptr, "ZLib inflate failed with code %d", r);
			}

			if (!write_position)
				retry_refill = true;
		}
		else // Algorithm none
		{
			ptrdiff_t szcopy = MIN(read_buffer.size(), write_buffer.size());
			memcpy(data_out.data(), data_in.data(), szcopy);
			read_position += szcopy;
			write_position += szcopy;
		}
	} 	while (retry_refill && !read_eof);

	return AMP_NO_ERROR;
}


amp_err_t*
amp_bucket_compression::read(
			amp_span* data_in,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	if (write_read_position >= write_position)
		AMP_ERR(refill(requested, scratch_pool));

	*data_in = write_buffer.min_subspan(write_read_position, MIN(requested, write_position - write_read_position));
	write_read_position += data_in->size_bytes();
	buf_position += data_in->size_bytes();

	if (data_in->size_bytes())
		return AMP_NO_ERROR;
	else
	{
		AMP_ASSERT(read_eof);

		return amp_err_create(AMP_EOF, nullptr, nullptr);
	}
}

amp_err_t*
amp_bucket_compression::peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool)
{
	if (write_read_position >= write_position && !no_poll)
		AMP_ERR(refill(AMP_READ_ALL_AVAIL, scratch_pool));

	*data = write_buffer.min_subspan(write_read_position, write_position - write_read_position);

	if (data->size_bytes() || !read_eof)
		return AMP_NO_ERROR;
	else
		return amp_err_create(AMP_EOF, nullptr, nullptr);
}

amp_off_t
amp_bucket_compression::get_position()
{
	return buf_position;
}