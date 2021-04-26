#include <amp_buckets.hpp>
#include <amp_src_git.hpp>

#include <string>

#ifdef _MSC_VER
#include <intrin.h>
#define __builtin_popcount __popcnt
#endif

using namespace amp;

static const amp::amp_bucket::amp_bucket_type git_packheader_type("amp.git.packheader");
static const amp::amp_bucket::amp_bucket_type git_packframe_type("amp.git.packframe");
static const amp::amp_bucket::amp_bucket_type git_packdelta_type("amp.git.packdelta");
static const amp::amp_bucket::amp_bucket_type git_chunkfile_type("amp.git.chunkfile");

amp_bucket_git_pack_header::amp_bucket_git_pack_header(amp_bucket_t* from, amp_allocator_t* allocator)
	: amp_bucket(&git_packheader_type, allocator)
{
	wrapped = from;
	position = 0;
	pack_version = 0;
	pack_object_count = 0;
}

void
amp_bucket_git_pack_header::destroy(amp_pool_t* scratch_pool)
{
	(*wrapped)->destroy(scratch_pool);

	amp_bucket::destroy(scratch_pool);
}

amp_err_t*
amp_bucket_git_pack_header::read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	AMP_UNUSED(requested);
	*data = amp_span();
	if (position < 12)
	{
		unsigned u1, u2;

		AMP_ERR(read_pack_info(&u1, &u2, scratch_pool));
	}

	return amp_err_create(AMP_EOF, nullptr, nullptr);
}

amp_err_t*
amp_bucket_git_pack_header::read_pack_info(
			unsigned* version,
			unsigned* object_count,
			amp_pool_t* scratch_pool)
{
	if (position < 12)
	{
		amp_span data;

		while (position < sizeof(buffer))
		{
			AMP_ERR((*wrapped)->read(&data, sizeof(buffer) - position, scratch_pool));

			memcpy(buffer + position, data.data(), data.size_bytes());
			position += data.size_bytes();
		}

		data = amp_span(buffer, sizeof(buffer));

		if (0 != memcmp("PACK", &data[0], 4))
			return amp_err_create(AMP_EGENERAL, nullptr, "File is not a packfile");

		pack_version = span_to_unsigned32_net(data.subspan(4));
		pack_object_count = span_to_unsigned32_net(data.subspan(8));
	}

	if (version)
		*version = pack_version;
	if (object_count)
		*object_count = pack_object_count;

	return AMP_NO_ERROR;
}

amp_bucket_git_pack_frame::amp_bucket_git_pack_frame(amp_bucket_t* from, amp_git_oid_type_t git_oid_type, amp_allocator_t* allocator)
	: amp_bucket(&git_packframe_type, allocator)
{
	wrapped = from;
	reader = nullptr;
	state = state::start;
	body_size = -1;
	position = 0;
	base_oid.type = git_oid_type;
	git_type = amp_git_object_none;
	delta_position = 0;
	frame_position = -1;
	delta_count = 0;
}

void amp_bucket_git_pack_frame::destroy(amp_pool_t* scratch_pool)
{
	amp_bucket_destroy(wrapped, scratch_pool);
	if (reader)
		amp_bucket_destroy(reader, scratch_pool);

	amp_bucket::destroy(scratch_pool);
}

amp_err_t*
amp_bucket_git_pack_frame::read_frame_info(
	amp_git_object_type_t* obj_type,
	int* deltas,
	amp_pool_t* scratch_pool)
{
	if (state < state::body)
	{
		const ptrdiff_t max_size_len = 1 + (64 - 4 + 6) / 7;

		while (state == state::start)
		{
			// In the initial state we use position to keep track of our
			// location withing the compressed length

			amp_span peeked;
			AMP_ERR((*wrapped)->peek(&peeked, false, scratch_pool));
			ptrdiff_t rq_len;

			if (peeked.size_bytes())
			{
				rq_len = 0;
				for (ptrdiff_t i = 0; i <= max_size_len && i < peeked.size_bytes(); i++)
				{
					rq_len++;
					if (!(peeked[i] & 0x80))
						break;
				}
				rq_len = MIN(rq_len, peeked.size_bytes());
			}
			else
				rq_len = 1;

			amp_span read;
			AMP_ERR((*wrapped)->read(&read, rq_len, scratch_pool));

			for (ptrdiff_t i = 0; i < read.size_bytes(); i++)
			{
				unsigned char uc = read[i];

				if (position == 0)
				{
					git_type = (amp_git_object_type_t)((uc >> 4) & 0x7);
					body_size = uc & 0xF;

					amp_off_t my_offs = (*wrapped)->get_position();
					if (my_offs >= 0)
						frame_position = my_offs - read.size();
				}
				else
					body_size |= (amp_off_t)(uc & 0x7F) << (4 + 7 * (position - 1));

				if (!(uc & 0x80))
				{
					if (position > max_size_len)
						return amp_err_create(AMP_EGENERAL, nullptr, "Git pack framesize overflows int64");

					if (git_type == 0)
						return amp_err_create(AMP_EGENERAL, nullptr, "Git pack frame 0 is invalid");
					else if (git_type == 5)
						return amp_err_create(AMP_EGENERAL, nullptr, "Git pack frame 5 is unsupported");

					AMP_ASSERT(i == read.size_bytes() - 1);
					state = state::size_done;
					position = 0;
				}
				else
					position++;
			}
		}

		while (state == state::size_done)
		{
			if (git_type == amp_git_delta_ref)
			{
				// Body starts with oid refering to the delta base
				ptrdiff_t base_len;

				if (git_type == amp_git_delta_ref || git_type == amp_git_delta_ofs)
					base_len = (base_oid.type == amp_git_oid_sha1) ? 20 : 32;
				else
					base_len = 0;

				AMP_ASSERT(position <= base_len);
				amp_span read;

				if (base_len > position)
					AMP_ERR((*wrapped)->read(&read, base_len - (ptrdiff_t)position, scratch_pool));

				if (read.size_bytes())
				{
					memcpy(base_oid.bytes + position, read.data(), read.size_bytes());
					position += read.size_bytes();
				}

				if (position >= base_len)
				{
					AMP_ASSERT(position == base_len);
					position = 0; // And now start the real body
					state = state::find_delta;
					break;
				}
			}
			else if (git_type == amp_git_delta_ofs)
			{
				// Body starts with negative offset of the delta base.
				const ptrdiff_t max_delta_size_len = 1 + (64 + 6) / 7;

				amp_span peeked;
				AMP_ERR((*wrapped)->peek(&peeked, false, scratch_pool));
				ptrdiff_t rq_len;

				if (peeked.size_bytes())
				{
					rq_len = 0;
					for (ptrdiff_t i = 0; i <= max_delta_size_len && i < peeked.size_bytes(); i++)
					{
						rq_len++;
						if (!(peeked[i] & 0x80))
							break;
					}
					rq_len = MIN(rq_len, peeked.size_bytes());
				}
				else
					rq_len = 1;

				amp_span read;
				AMP_ERR((*wrapped)->read(&read, rq_len, scratch_pool));

				for (ptrdiff_t i = 0; i < read.size_bytes(); i++)
				{
					unsigned char uc = read[i];

					if (position)
						delta_position = (delta_position + 1) << 7;

					delta_position |= (amp_off_t)(uc & 0x7F);
					position++;

					if (!(uc & 0x80))
					{
						if (position > max_delta_size_len)
							return amp_err_create(AMP_EGENERAL, nullptr, "Git pack delta referene overflows 64 bit integer");
						else if (delta_position > frame_position)
							return amp_err_create(AMP_EGENERAL, nullptr, "Delta position must point to earlier object in file");

						AMP_ASSERT(i == read.size_bytes() - 1);
						state = state::find_delta;
						position = 0;
						delta_position = frame_position - delta_position;
						reader = AMP_ALLOCATOR_NEW(amp_bucket_decompress, allocator,
												   amp_bucket_block_create(wrapped, allocator),
												   amp_compression_algorithm_zlib, -1, allocator);
					}
				}
			}
			else
			{
				position = 0; // The real body starts right now
				state = state::body;
				reader = AMP_ALLOCATOR_NEW(amp_bucket_decompress, allocator,
										   amp_bucket_block_create(wrapped, allocator),
										   amp_compression_algorithm_zlib, -1, allocator);
				break;
			}
		}

		while (state == state::find_delta)
		{
			amp_bucket_t* base_reader = nullptr;

			if (git_type == amp_git_delta_ofs)
			{
				amp_bucket_t* src = nullptr;
				// amp_file stream and its reader can be duplicated. It was designed for this scenario
				amp_err_t* err = (*wrapped)->duplicate(&src, true, scratch_pool);

				// Can't use limit stream here, as that would break recursive fetching
				if (!err && src)
				{
					amp_off_t to_skip = delta_position;

					err = (*src)->reset(scratch_pool);

					amp_off_t skipped;
					while (!err && to_skip > 0)
					{
						err = (*src)->read_skip(&skipped, to_skip, scratch_pool);
						to_skip -= skipped;
					}

					base_reader = AMP_ALLOCATOR_NEW(amp_bucket_git_pack_frame, allocator, src, base_oid.type, allocator);
					src = nullptr;
				}

				if (src)
					amp_bucket_destroy(src, scratch_pool);

				if (err)
					return amp_err_create(AMP_ERR_WAIT_CONN, err, "Can't obtain delta reference via inner support");
			}
			else
				return amp_err_create(AMP_ERR_WAIT_CONN, nullptr, "Can't obtain delta reference (via oid not implemented yet)");

			auto frame_reader = dynamic_cast<amp_bucket_git_pack_frame*>(static_cast<amp_bucket*>(base_reader));

			if (frame_reader)
			{
				amp_git_object_type_t base_type;
				AMP_ERR(frame_reader->read_frame_info(&base_type, &delta_count, scratch_pool));
				delta_count++;
				state = state::body;
				git_type = base_type; // type is now resolved

				reader = AMP_ALLOCATOR_NEW(amp_bucket_git_delta, allocator,
										   reader,
										   frame_reader,
										   allocator);
			}
			else
				AMP_ASSERT(0 && "Unexpected");
		}
	}

	if (obj_type)
		*obj_type = git_type;
	if (deltas)
		*deltas = delta_count;

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_git_pack_frame::read(
	amp_span* data,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	if (state < state::body)
		AMP_ERR(read_frame_info(nullptr, nullptr, scratch_pool));

	return amp_err_trace((*reader)->read(data, requested, scratch_pool));
}

amp_err_t*
amp_bucket_git_pack_frame::read_until_eol(
	amp_span* data,
	amp_newline_t* found,
	amp_newline_t acceptable,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	if (state < state::body)
		AMP_ERR(read_frame_info(nullptr, nullptr, scratch_pool));

	return amp_err_trace((*reader)->read_until_eol(data, found, acceptable, requested, scratch_pool));
}

amp_err_t*
amp_bucket_git_pack_frame::peek(
	amp_span* data,
	bool no_poll,
	amp_pool_t* scratch_pool)
{
	if (state < state::body)
	{
		if (no_poll)
		{
			*data = amp_span();
			return AMP_NO_ERROR;
		}

		AMP_ERR(read_frame_info(nullptr, nullptr, scratch_pool));
	}

	return amp_err_trace((*reader)->peek(data, no_poll, scratch_pool));
}

amp_err_t*
amp_bucket_git_pack_frame::reset(
	amp_pool_t* scratch_pool)
{
	if (frame_position < 0)
		return amp_err_create(AMP_ERR_NOT_SUPPORTED, nullptr, nullptr); // Can't seek to frame position

	AMP_ERR((*wrapped)->reset(scratch_pool));

	if (reader)
	{
		(*reader)->destroy(scratch_pool);
		reader = nullptr;
	}

	state = state::start;
	position = 0;
	body_size = -1;

	amp_off_t skipped;
	AMP_ERR((*wrapped)->read_skip(&skipped, this->frame_position, scratch_pool));
	AMP_ASSERT(skipped == frame_position);
	git_type = amp_git_object_none;
	delta_position = 0;
	frame_position = -1;
	delta_count = 0;

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_git_pack_frame::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	if (state < state::body)
		AMP_ERR(read_frame_info(nullptr, nullptr, scratch_pool));

	AMP_ASSERT(state == state::body);

	if (delta_count)
		return amp_err_trace((*reader)->read_remaining_bytes(remaining, scratch_pool));
	else
	{
		*remaining = body_size - (*reader)->get_position();

		return AMP_NO_ERROR;
	}
}

amp_off_t
amp_bucket_git_pack_frame::get_position()
{
	if (state < state::body)
		return 0;

	return (*reader)->get_position();
}

static int bit_count(unsigned char c)
{
	return __builtin_popcount(c);
	//return
	//	((c & 0x80) >> 7) +
	//	((c & 0x40) >> 6) +
	//	((c & 0x20) >> 5) +
	//	((c & 0x10) >> 4) +
	//	((c & 0x08) >> 3) +
	//	((c & 0x04) >> 2) +
	//	((c & 0x02) >> 1) +
	//	((c & 0x01) >> 0);
}

amp_bucket_git_delta::amp_bucket_git_delta(
	amp_bucket_t* delta_src,
	amp_bucket_t* delta_base,
	amp_allocator_t* allocator)
	: amp_bucket(&git_packdelta_type, allocator)
{
	AMP_ASSERT(delta_src && delta_base);
	src = delta_src;
	base = delta_base;
	length = 0;
	state = state::start;
	position = 0;
	p0 = 0;
	copy_size = copy_offset = 0;
	memset(buffer, 0, sizeof(buffer));
}

void
amp_bucket_git_delta::destroy(amp_pool_t* scratch_pool)
{
	(*src)->destroy(scratch_pool);
	(*base)->destroy(scratch_pool);
	amp_bucket::destroy(scratch_pool);
}

amp_err_t*
amp_bucket_git_delta::advance(amp_pool_t* scratch_pool)
{
	amp_span data;

	while (state == state::start)
	{
		while (p0 >= 0)
		{
			// This initial loop re-uses length to collect the base size, as we don't have that
			// value at this point anyway
			AMP_ERR((*src)->read(&data, 1, scratch_pool));
			unsigned char uc = data[0];

			const int shift = (p0 * 7);
			length |= (amp_off_t)(uc & 0x7F) << shift;
			p0++;

			if (!(data[0] & 0x80))
			{
				amp_off_t base_size;
				auto err = amp_err_trace((*base)->read_remaining_bytes(&base_size, scratch_pool));

				if (AMP_IS_BUCKET_READ_ERROR(err))
					return err;
				else
					amp_err_clear(err);

				if (!err && base_size != length)
					return amp_err_createf(AMP_EGENERAL, nullptr, "Expected delta base size (%d) doesn't match source size (%d)", (int)length, (int)base_size);

				length = 0;
				p0 = -1;
			}
		}
		while (p0 < 0)
		{
			AMP_ERR((*src)->read(&data, 1, scratch_pool));
			unsigned char uc = data[0];

			const int shift = ((-1 - p0) * 7);
			length |= (amp_off_t)(uc & 0x7F) << shift;
			p0--;

			if (!(data[0] & 0x80))
			{
				p0 = 0;
				state = state::init;
			}
		}
	}

	while (state == state::init)
	{
		if (p0)
		{
			ptrdiff_t want = bit_count(buffer[0]) - p0;
			AMP_ERR((*src)->read(&data, want, scratch_pool));
			memcpy(&buffer[p0], data.data(), data.size_bytes());

			if (data.size() < want)
				continue;

			data = amp_span(buffer, sizeof(buffer));
			want += p0;
			p0 = 0;
		}
		else
		{
			ptrdiff_t want;
			bool peeked = false;

			AMP_ERR((*src)->peek(&data, false, scratch_pool));

			if (data.size_bytes())
			{
				peeked = true;
				if (data[0] & 0x80)
					want = bit_count(data[0]); // use 0x80 bit for reading cmd itself
				else
					want = 1;
			}
			else
				want = 1;

			AMP_ERR((*src)->read(&data, want, scratch_pool));

			if (!peeked && (data[0] & 0x80))
				want = bit_count(data[0]); // Maybe not peeked. Set data correctly from read data

			if (data.size_bytes() < want)
			{
				memcpy(buffer, data.data(), data.size_bytes());
				p0 = data.size_bytes();
				continue;
			}
		}

		unsigned char uc = data[0];
		if (!(uc & 0x80))
		{
			state = state::src_copy;
			copy_size = (uc & 0x7F);
		}
		else
		{
			copy_offset = 0;
			copy_size = 0;

			const unsigned char* pU = (const unsigned char*)data.data() + 1;

			if (uc & 0x01)
				copy_offset |= (unsigned)(*pU++) << 0;
			if (uc & 0x02)
				copy_offset |= (unsigned)(*pU++) << 8;
			if (uc & 0x04)
				copy_offset |= (unsigned)(*pU++) << 16;
			if (uc & 0x08)
				copy_offset |= (unsigned)(*pU++) << 24;

			if (uc & 0x10)
				copy_size |= (unsigned)(*pU++) << 0;
			if (uc & 0x20)
				copy_size |= (unsigned)(*pU++) << 8;
			if (uc & 0x40)
				copy_size |= (unsigned)(*pU++) << 16;

			if (!copy_size)
				copy_size = 0x10000;

			//if (!copy_offset)
			//	copy_offset = -1; // No offset

			state = state::base_copy;
		}
	}

	while ((state == state::base_copy) && (copy_offset >= 0))
	{
		amp_off_t cp = (*base)->get_position();

		if (copy_offset < cp)
		{
			AMP_ERR((*base)->reset(scratch_pool));
			cp = 0;
		}

		while (cp < copy_offset)
		{
			amp_off_t skipped;
			auto err = (*base)->read_skip(&skipped, copy_offset - cp, scratch_pool);
			if (AMP_ERR_IS_EOF(err))
				return amp_err_createf(AMP_EGENERAL, err, "Unexpected seek failure to base stream position %d", (int)copy_offset);
			else
				AMP_ERR(err);

			cp += skipped;
		}
		copy_offset = -1;
	}
	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_git_delta::read(
	amp_span* data,
	ptrdiff_t requested,
	amp_pool_t* scratch_pool)
{
	auto err = advance(scratch_pool);
	if (AMP_ERR_IS_EOF(err))
		return amp_err_create(AMP_EGENERAL, err, "Unexpected EOF on command stream");
	else
		AMP_ERR(err);

	AMP_ASSERT(state == state::base_copy || state == state::src_copy || state == state::eof);

	if (state == state::base_copy)
	{
		err = amp_err_trace((*base)->read(data, MIN(requested, copy_size), scratch_pool));

		if (err)
		{
			if (AMP_ERR_IS_EOF(err))
				return amp_err_create(AMP_EGENERAL, err, "Unexpected EOF on base stream");
			else
				return err;
		}

		position += data->size_bytes();
		copy_size -= data->size_bytes();

		if (copy_size == 0)
		{
			if (position == length)
				state = state::eof;
			else
				state = state::init;
			p0 = 0;
		}
		return AMP_NO_ERROR;
	}
	else if (state == state::src_copy)
	{
		err = amp_err_trace((*src)->read(data, MIN(requested, copy_size), scratch_pool));

		if (err)
		{
			if (AMP_ERR_IS_EOF(err))
				return amp_err_create(AMP_EGENERAL, err, "Unexpected EOF on src stream");
			else
				return err;
		}

		position += data->size_bytes();
		copy_size -= data->size_bytes();

		if (copy_size == 0)
		{
			if (position == length)
				state = state::eof;
			else
				state = state::init;
			p0 = 0;
		}
		return AMP_NO_ERROR;
	}
	else if (state == state::eof)
	{
		return amp_err_create(AMP_EOF, nullptr, nullptr);
	}

	return amp_err_create(AMP_EGENERAL, nullptr, nullptr);
}

amp_err_t*
amp_bucket_git_delta::peek(
	amp_span* data,
	bool no_poll,
	amp_pool_t* scratch_pool)
{
	if (!no_poll)
		AMP_ERR(advance(scratch_pool));

	if (state == state::base_copy)
	{
		AMP_ERR((*base)->peek(data, no_poll, scratch_pool));

		if (copy_size < data->size_bytes())
			*data = data->subspan(0, copy_size);

		return AMP_NO_ERROR;
	}
	else if (state == state::src_copy)
	{
		AMP_ERR((*src)->peek(data, no_poll, scratch_pool));

		if (copy_size < data->size_bytes())
			*data = data->subspan(0, copy_size);

		return AMP_NO_ERROR;
	}
	else if (state == state::eof)
	{
		return amp_err_create(AMP_EOF, nullptr, nullptr);
	}
	else
	{
		*data = amp_span();
		return AMP_NO_ERROR;
	}
}

amp_err_t*
amp_bucket_git_delta::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	AMP_ERR(advance(scratch_pool));

	if (state < state::init)
		return amp_err_create(AMP_EAGAIN, nullptr, nullptr);

	*remaining = (length - position);
	return AMP_NO_ERROR;
}

amp_off_t
amp_bucket_git_delta::get_position()
{
	if (state < state::init)
		return 0;

	return position;
}

amp_bucket_git_chunk_file::amp_bucket_git_chunk_file(amp_bucket_t* from, const char* expected_signature, ptrdiff_t extra_header_bytes, amp_allocator_t* allocator)
	: amp_bucket(&git_chunkfile_type, allocator)
{
	AMP_ASSERT(from && strlen(expected_signature) == 4 && extra_header_bytes >= 0);
	wrapped = from;
	header_bytes = extra_header_bytes;
	memcpy(hdr, expected_signature, sizeof(hdr));
	chunk_items = nullptr;
	chunk_count = 0;
	position = 0;

	buffer = extra_header_bytes ? span<char>(amp_allocator_alloc_n<char>(header_bytes, allocator), header_bytes) : span<char>();
}

void
amp_bucket_git_chunk_file::destroy(amp_pool_t* scratch_pool)
{
	(*wrapped)->destroy(scratch_pool);
	if (buffer.data())
		(*allocator)->free(buffer.data());
	if (chunk_items)
		(*allocator)->free(chunk_items);

	amp_bucket::destroy(scratch_pool);
}

amp_err_t*
amp_bucket_git_chunk_file::read(
		amp_span* data,
		ptrdiff_t requested,
		amp_pool_t* scratch_pool)
{
	AMP_UNUSED(requested);
	*data = amp_span();
	AMP_ERR(read_info(nullptr, nullptr, nullptr, scratch_pool));

	return amp_err_create(AMP_EOF, nullptr, nullptr);
}

amp_err_t*
amp_bucket_git_chunk_file::read_info(amp_git_oid_type_t* oid_type, int* version, int* chunks, amp_pool_t* scratch_pool)
{
	while (position < (7 + header_bytes))// || position < (7 + header_bytes + 12 * chunk_count))
	{
		amp_span data;
		AMP_ERR((*wrapped)->read(&data, 7 + header_bytes - position, scratch_pool));

		if (position < 4 && memcmp(data.data(), hdr + position, MIN(4 - position, data.size_bytes() - position)))
			return amp_err_createf(AMP_EGENERAL, nullptr, "File does not have expected chunk type '%4s'", hdr);

		if (position < 5 && (data.size_bytes() + position) >= 5)
			hdr[0] = data[4 - position]; // oid type
		if (position < 6 && (data.size_bytes() + position) >= 6)
			hdr[1] = data[5 - position]; // version
		if (position < 7 && (data.size_bytes() + position) >= 7)
			chunk_count = (unsigned char)data[6 - position]; // chunks

		if ((data.size_bytes() + position) >= 7)
			memcpy(buffer.data() + MAX(0, position - 7), data.data() + MIN(0, 7 - position), data.size_bytes() + MIN(0, position - 7));

		position += data.size_bytes();

		chunk_items = amp_allocator_alloc_n<chunk_t>(chunk_count + 1, allocator);
		AMP_ASSERT(sizeof(chunk_items[0]) > 12);
	}
	const ptrdiff_t chunkdata_sz = 12 * (chunk_count + 1);
	while (position < (7 + header_bytes + chunkdata_sz))
	{
		ptrdiff_t p = position - 7 - header_bytes;
		char* pData = (char*)chunk_items; // Use chunk items as raw buffer, until read

		amp_span data;
		AMP_ERR((*wrapped)->read(&data, chunkdata_sz - p, scratch_pool));

		memcpy(pData + p, data.data(), data.size_bytes());
		position += data.size_bytes();

		if (position >= (7 + header_bytes + chunkdata_sz))
		{
			AMP_ASSERT(position == (7 + header_bytes + chunkdata_sz));

			for (int i = chunk_count; i >= 0; i--)
			{
				chunk_items[i].offset = span_to_unsigned64_net(amp_span(pData + 12 * i + 4, sizeof(amp_off_t)));
				memcpy(chunk_items[i].id, pData + (12 * i), sizeof(chunk_items[0].id));
			}
		}
	}

	if (oid_type)
		*oid_type = (amp_git_oid_type_t)hdr[0];
	if (version)
		*version = (unsigned char)hdr[1];
	if (chunks)
		*chunks = chunk_count;

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_git_chunk_file::read_chunk_bucket(amp_bucket_t** bucket, const char* chunk_name, amp_pool_t* scratch_pool)
{
	AMP_ERR(read_info(nullptr, nullptr, nullptr, scratch_pool));

	int idx = -1;
	if (strlen(chunk_name) == 4)
	{
		for (int i = 0; i < chunk_count; i++)
		{
			if (!memcmp(chunk_items[i].id, chunk_name, 4))
			{
				idx = i;
				break;
			}
		}
	}

	if (idx < 0)
		return amp_err_createf(AMP_EGENERAL, nullptr, "Chunk '%s' not found in file", chunk_name);

	amp_bucket_t* p;
	AMP_ERR((*wrapped)->duplicate(&p, true, scratch_pool));

	amp_err_t* err = (*p)->reset(scratch_pool);
	amp_off_t to_skip = chunk_items[idx].offset;
	while (!err && to_skip > 0)
	{
		amp_off_t skipped;
		err = (*p)->read_skip(&skipped, to_skip, scratch_pool);

		if (!err)
			to_skip -= skipped;
	}

	if (err)
	{
		amp_bucket_destroy(p, scratch_pool);
		return amp_err_trace(err);
	}

	*bucket = AMP_ALLOCATOR_NEW(amp_bucket_limit, allocator, p, chunk_items[idx + 1].offset - chunk_items[idx].offset, allocator);
	return AMP_NO_ERROR;
}

const char*
amp_src_git_type_name(amp_git_object_type_t type)
{
	static const char* types[8] = {
		"<no-type>", // None
		"commit",
		"tree",
		"blob",
		"tag",
		"<invalid-type>", // invalid

		"<offset-delta>", // delta
		"<oid-delta>", // delta
	};

	return types[type & 0x7];
}


AMP_DECLARE(const char*)
amp_src_git_create_header(
	amp_git_object_type_t type,
	amp_off_t size,
	amp_pool_t* result_pool)
{
	return amp_psprintf(result_pool, "%s %I64d", amp_src_git_type_name(type), size);
}

unsigned amp::span_to_unsigned32_net(amp_span span)
{
	unsigned char* pd = (unsigned char*)span.data();
	AMP_ASSERT(span.size_bytes() >= 32 / 8);

	return (unsigned)pd[0] << 24
		| (unsigned)pd[1] << 16
		| (unsigned)pd[2] << 8
		| (unsigned)pd[3];
}

unsigned long long amp::span_to_unsigned64_net(amp_span span)
{
	unsigned char* pd = (unsigned char*)span.data();
	AMP_ASSERT(span.size_bytes() >= 64 / 8);

	return (unsigned long long)pd[0] << 56
		| (unsigned long long)pd[1] << 48
		| (unsigned long long)pd[2] << 40
		| (unsigned long long)pd[3] << 32
		| (unsigned long long)pd[4] << 24
		| (unsigned long long)pd[5] << 16
		| (unsigned long long)pd[6] << 8
		| (unsigned long long)pd[7];
}

unsigned amp::span_to_unsigned32_le(amp_span span)
{
	unsigned char* pd = (unsigned char*)span.data();
	AMP_ASSERT(span.size_bytes() >= 32 / 8);

	return (unsigned)pd[0]
		| (unsigned)pd[1] << 8
		| (unsigned)pd[2] << 16
		| (unsigned)pd[3] << 24;
}

unsigned long long amp::span_to_unsigned64_le(amp_span span)
{
	unsigned char* pd = (unsigned char*)span.data();
	AMP_ASSERT(span.size_bytes() >= 64 / 8);

	return (unsigned long long)pd[0]
		| (unsigned long long)pd[1] << 8
		| (unsigned long long)pd[2] << 16
		| (unsigned long long)pd[3] << 24
		| (unsigned long long)pd[4] << 32
		| (unsigned long long)pd[5] << 40
		| (unsigned long long)pd[6] << 48
		| (unsigned long long)pd[7] << 56;
}


unsigned amp::span_to_unsigned32_host(amp_span span)
{
	AMP_ASSERT(span.size_bytes() >= 4);
	return *(unsigned*)span.data();
}

unsigned long long amp::span_to_unsigned64_host(amp_span span)
{
	AMP_ASSERT(span.size_bytes() >= 8);
	return *(unsigned long long*)span.data();
}
