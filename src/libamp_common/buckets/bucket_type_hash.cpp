#ifdef _WIN32
#include <Windows.h>
#include <bcrypt.h>
#else
// TODO: Add OPENSSL variant
#endif
#include <zlib.h>

#include <amp_buckets.hpp>

using namespace amp;

static amp::amp_bucket::amp_bucket_type hash_bucket_type("amp.hash");

amp_hash_result_t*
amp_hash_result_create(
	amp_hash_algorithm_t algorithm,
	amp_pool_t* result_pool)
{
	size_t bytes;
	switch (algorithm)
	{
		case amp_hash_algorithm_md5:
			bytes = 16;
			break;
		case amp_hash_algorithm_sha1:
			bytes = 20;
			break;
		case amp_hash_algorithm_sha256:
			bytes = 32;
			break;
		case amp_hash_algorithm_adler32:
		case amp_hash_algorithm_crc32:
			bytes = (32 / 8);
			break;
		default:
			AMP_ASSERT(algorithm && "is unsupported");
			bytes = 64;
			break;
	}

	amp_hash_result_t* res = (amp_hash_result_t*)amp_pcalloc(sizeof(amp_hash_result_t) + bytes, result_pool);
	res->hash_algorithm = algorithm;
	res->hash_bytes = bytes;
	return res;
}

amp_bucket_hash::amp_bucket_hash(
				amp_bucket_t* wrap_bucket,
				amp_hash_result_t* fill_result,
				const amp_hash_result_t* expect_result,
				amp_allocator_t* allocator)
	: amp_bucket(&hash_bucket_type, allocator)
{
	AMP_ASSERT(wrap_bucket && (!fill_result ^ !expect_result)
				&& (!fill_result || fill_result->hash_bytes >= 4)
			   && (!expect_result || expect_result->hash_bytes >= 4)
	);

	wrapped = wrap_bucket;
	algorithm = fill_result ? fill_result->hash_algorithm : expected_result->hash_algorithm;

	new_result = fill_result;
	expected_result = expect_result ? (amp_hash_result_t*)amp_allocator_pmemdup(expected_result, sizeof(*expected_result), allocator) : nullptr;
	if (new_result)
		memset(new_result->bytes, 0, new_result->hash_bytes);

	p1 = p2 = p3 = nullptr;
	hashed_bytes = 0;

	setupHashing();
	done = false;
}

void
amp_bucket_hash::destroy(amp_pool_t* scratch_pool)
{
	finishHashing(false);
	if (expected_result)
		(*allocator)->free(expected_result);
	(*wrapped)->destroy(scratch_pool);
	amp_bucket::destroy(scratch_pool);
}

void amp_bucket_hash::setupHashing()
{
	hashed_bytes = 0;
	if (algorithm == amp_hash_algorithm_adler32 || algorithm == amp_hash_algorithm_crc32 || algorithm == amp_hash_algorithm_none)
	{
		p3 = amp_allocator_calloc<uLong>(allocator);
		return;
	}
#ifdef _WIN32
	BCRYPT_ALG_HANDLE hAlg;
	const wchar_t* alg;

	switch (this->algorithm)
	{
		case amp_hash_algorithm_md5:
			alg = BCRYPT_MD5_ALGORITHM;
			break;
		case amp_hash_algorithm_sha1:
			alg = BCRYPT_SHA1_ALGORITHM;
			break;
		case amp_hash_algorithm_sha256:
			alg = BCRYPT_SHA256_ALGORITHM;
			break;
		default:
			AMP_ASSERT(0 && "bad algorithm value");
			this->algorithm = amp_hash_algorithm_none;
			return;
	}

	long st = BCryptOpenAlgorithmProvider(&hAlg, alg, nullptr, 0);

	if (st < 0)
	{
		this->algorithm = amp_hash_algorithm_none;
		return; // Failed
	}

	p1 = hAlg;

	DWORD objectLength;
	ULONG cbDataSet;
	if (BCryptGetProperty(
		hAlg,
		BCRYPT_OBJECT_LENGTH,
		(PBYTE)&objectLength,
		sizeof(objectLength),
		&cbDataSet,
		0) < 0)
	{
		this->algorithm = amp_hash_algorithm_none;
		return; // Failed
	}

	p3sz = objectLength;
	p3 = (*allocator)->alloc(p3sz);
	memset(p3, 0, p3sz);

	if (BCryptCreateHash(hAlg, &p2, (PUCHAR)p3, p3sz, nullptr, 0, 0) < 0)
	{
		this->algorithm = amp_hash_algorithm_none;
		return; // Failed
	}
#endif
}

amp_err_t*
amp_bucket_hash::finishHashing(bool useResult)
{
	if (algorithm == amp_hash_algorithm_none)
		return amp_err_create(AMP_EGENERAL, nullptr, "Unable to setup hashing");

	if (useResult && new_result)
		new_result->original_size = hashed_bytes;

	if (algorithm == amp_hash_algorithm_adler32 || algorithm == amp_hash_algorithm_crc32)
	{
		if (useResult)
		{
			uLong result = *reinterpret_cast<uLong*>(p3);
			int r;
			unsigned char* pResult = new_result ? new_result->bytes : (unsigned char*)&r;

			pResult[0] = (result >> 24) & 0xFF;
			pResult[1] = (result >> 16) & 0xFF;
			pResult[2] = (result >> 8) & 0xFF;
			pResult[3] = (result >> 0) & 0xFF;

			if (expected_result && memcmp(expected_result->bytes, pResult, 4))
				return amp_err_create(AMP_EGENERAL, nullptr, "Checksum mismatch");
		}

		if (p3)
		{
			(*allocator)->free(p3);
			p3 = nullptr;
		}
		return AMP_NO_ERROR;
	}
#ifdef _WIN32
	if (useResult)
	{
		unsigned char dummyBuf[64];
		size_t sz = new_result ? new_result->hash_bytes : expected_result->hash_bytes;
		unsigned char* pResult = new_result ? new_result->bytes : dummyBuf;

		AMP_ASSERT(sizeof(dummyBuf) >= sz);

		if (BCryptFinishHash(p2, pResult, new_result ? new_result->hash_bytes : expected_result->hash_bytes, 0) < 0)
			return amp_err_create(AMP_EGENERAL, nullptr, "Unable to finish hashing");

		if (expected_result && memcmp(expected_result->bytes, pResult, sz))
			return amp_err_create(AMP_EGENERAL, nullptr, "Checksum mismatch");
	}

	if (p2)
	{
		BCryptDestroyHash(p2);
		p2 = nullptr;
	}
	if (p1)
	{
		BCryptCloseAlgorithmProvider(p1, 0);
		p1 = nullptr;
	}
#endif
	if (p3)
	{
		(*allocator)->free(p3);
		p3 = nullptr;
	}
	return AMP_NO_ERROR;
}

void
amp_bucket_hash::hashData(amp_span span)
{
	hashed_bytes += span.size_bytes();

	if (algorithm == amp_hash_algorithm_none)
		return; // Already in error
	else if ((algorithm == amp_hash_algorithm_adler32 || algorithm == amp_hash_algorithm_crc32))
	{
		uLong* pValue = reinterpret_cast<uLong*>(p3);

		if (algorithm == amp_hash_algorithm_crc32)
			*pValue = crc32(*pValue, (const Bytef*)span.data(), span.size_bytes());
		else
			*pValue = adler32(*pValue, (const Bytef*)span.data(), span.size_bytes());
		return;
	}
#ifdef _WIN32
	if (!p2 || BCryptHashData(p2, (PUCHAR)span.data(), span.size_bytes(), 0) < 0)
	{
		algorithm = amp_hash_algorithm_none;
	}
#else
#endif
}

amp_err_t*
amp_bucket_hash::read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	amp_err_t* err = amp_err_trace(
		(*wrapped)->read(data, requested, scratch_pool));

	if (!err)
		hashData(*data);
	else if (AMP_ERR_IS_EOF(err))
		return amp_err_compose_create(
			finishHashing(true),
			err);

	return err;
}

amp_err_t*
amp_bucket_hash::read_until_eol(
			amp_span* data,
			amp_newline_t* found,
			amp_newline_t acceptable,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool)
{
	amp_err_t* err = amp_err_trace(
		(*wrapped)->read_until_eol(data, found, acceptable,
								   requested, scratch_pool));

	if (!err)
		hashData(*data);
	else if (AMP_ERR_IS_EOF(err))
		return amp_err_compose_create(
			finishHashing(true),
			err);

	return err;
}

amp_err_t*
amp_bucket_hash::peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool)
{
	return
		amp_err_trace((*wrapped)->peek(data, no_poll, scratch_pool));
}

amp_err_t*
amp_bucket_hash::reset(
	amp_pool_t* scratch_pool)
{
	AMP_ERR((*wrapped)->reset(scratch_pool));

	amp_err_clear(finishHashing(false));
	setupHashing();

	return AMP_NO_ERROR;
}

amp_err_t*
amp_bucket_hash::read_remaining_bytes(
	amp_off_t* remaining,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace((*wrapped)->read_remaining_bytes(remaining, scratch_pool));
}

amp_off_t
amp_bucket_hash::get_position()
{
	return hashed_bytes;
}

amp_err_t*
amp_bucket_hash::duplicate(
	amp_bucket_t** result,
	bool for_reset,
	amp_pool_t* scratch_pool)
{
	amp_bucket_t* wr;
	AMP_ERR((*wrapped)->duplicate(&wr, for_reset, scratch_pool));

	if (expected_result)
	{
		*result = AMP_ALLOCATOR_NEW(amp_bucket_hash, allocator, wr, nullptr, expected_result, allocator);
		return AMP_NO_ERROR;
	}
	else
	{
		// No need to hash if nobody can access the result
		*result = wr;
		return AMP_NO_ERROR;
	}
}

amp_err_t*
amp_bucket_hash_create(
	amp_bucket_t** new_bucket,
	amp_hash_result_t** hash_result,
	amp_bucket_t* wrapped_bucket,
	amp_hash_algorithm_t algorithm,
	const amp_hash_result_t* expected_result,
	amp_allocator_t* allocator,
	amp_pool_t* result_pool)
{
	if (hash_result)
		*hash_result = amp_hash_result_create(algorithm, result_pool);

	*new_bucket = AMP_ALLOCATOR_NEW(amp_bucket_hash, allocator,
									wrapped_bucket, hash_result ? *hash_result : nullptr,
									expected_result, allocator);
	return AMP_NO_ERROR;
}

const char*
amp_hash_result_to_cstring(
	amp_hash_result_t* result,
	amp_boolean_t for_display,
	amp_pool_t* result_pool)
{
	AMP_ASSERT(result);

	if (!result)
		return nullptr;

	if (result->hash_algorithm == amp_hash_algorithm_none)
		return "";

	if (for_display)
	{
		bool all0 = true;
		for (size_t i = 0; i < result->hash_bytes; i++)
		{
			if (result->bytes[i])
			{
				all0 = false;
				break;
			}
		}
		if (all0)
			return "";
	}

	char* rs = amp_palloc_n<char>(result->hash_bytes * 2 + 1, result_pool);

	for (size_t i = 0; i < result->hash_bytes; i++)
	{
		int b = result->bytes[i];

		rs[2 * i] = "0123456789abcdef"[b >> 4];
		rs[2 * i + 1] = "0123456789abcdef"[b & 0xF];
	}
	rs[result->hash_bytes * 2] = 0;
	return rs;
}
