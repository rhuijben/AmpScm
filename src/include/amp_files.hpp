#include "amp_types.hpp"
#pragma once

#include <memory>
#include <atomic>
#ifdef _WIN32

#ifndef _WINDOWS_

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601
#endif

#ifndef NOUSER
#define NOUSER
#endif
#ifndef NOMCX
#define NOMCX
#endif
#ifndef NOIME
#define NOIME
#endif

#include <windows.h>
#endif

#endif

#include "amp_pools.hpp"
#include "amp_files.h"
#include "amp_span.hpp"

namespace amp
{
	class amp_file;
	class amp_file_handle;
}

struct amp_file_handle_t
{
protected:
	std::atomic_int32_t user_count;

#ifdef _WIN32
	HANDLE file_handle;
#else
	int file_descriptor;
#endif	

	AMP__PUBLIC_ACCESSOR_DECLARE(amp_file_handle)
};

struct amp_file_t
{
protected:
	amp_file_handle_t* file_handle;

	AMP__PUBLIC_ACCESSOR_DECLARE(amp_file)
};

namespace amp
{
	class amp_file_handle : public amp_file_handle_t
	{
	public:
#ifdef _WIN32
		amp_file_handle(HANDLE handle)
		{
			AMP_ASSERT(handle && handle != INVALID_HANDLE_VALUE);
			file_handle = handle;
		}
#else
		amp_file_handle(int filedes)
		{
			AMP_ASSERT(handle != -1);
			file_descriptor = filedes;
		}
#endif

	public:
		amp_file_handle* add_ref();
		void destroy();

		amp_err_t* explicit_destroy();

	public:
		amp_err_t* read(ptrdiff_t* bytes_read, amp_off_t offset, span<char> buffer) const noexcept;
		amp_err_t* get_current_size(amp_off_t* size) const noexcept;
		amp_err_t* write_full(amp_off_t offset, amp_span data) const noexcept;
		amp_err_t* truncate(amp_off_t offset) const noexcept;
		amp_err_t* flush(bool force_to_disk) const noexcept;


	public:
		amp_err_t* calculate_seek(amp_off_t& file_pos, amp_off_t offset) const noexcept
		{
			AMP_ASSERT(offset >= 0);

			if (offset < 0)
				return amp_err_create(AMP_BADARG, nullptr, nullptr);
			else if (offset == 0 || offset <= file_pos)
			{
				file_pos = offset;
				return AMP_NO_ERROR;
			}

			amp_off_t sz;
			AMP_ERR(get_current_size(&sz));

			if (offset <= sz)
				file_pos = offset;
			else
				return amp_err_create(AMP_EOF, nullptr, "Offset behind file");

			return AMP_NO_ERROR;
		}
	};

	class amp_file : public amp_file_t, amp_pool_managed
	{
	private:
		amp_off_t buf_position;
	public:
		amp_file(amp_file_handle_t* handle, amp_pool_t* pool)
			: amp_pool_managed(pool)
		{
			file_handle = (*handle)->add_ref();
			buf_position = 0;

			destroy_with_pool();
		}

		~amp_file()
		{
			destroy(nullptr);
		}

		virtual void destroy(amp_pool_t*) override
		{
			auto r = file_handle;
			file_handle = nullptr;

			if (r)
				(*r)->destroy();
		}

	public:
		amp_err_t* read(ptrdiff_t* bytes_read, span<char> buffer) noexcept
		{
			ptrdiff_t b_read;

			AMP_ERR((*file_handle)->read(&b_read, buf_position, buffer));

			buf_position += b_read;
			*bytes_read = b_read;
			return AMP_NO_ERROR;
		}

		amp_err_t* get_current_size(amp_off_t* size) noexcept
		{
			return amp_err_trace((*file_handle)->get_current_size(size));
		}

		constexpr amp_off_t get_current_position() const noexcept
		{
			return buf_position;
		}

		amp_err_t* write_full(amp_span buffer) noexcept
		{
			AMP_ERR((*file_handle)->write_full(buf_position, buffer));

			buf_position += buffer.size_bytes();
			return AMP_NO_ERROR;
		}

		amp_err_t* truncate() noexcept
		{
			return amp_err_trace((*file_handle)->truncate(buf_position));
		}

		amp_err_t* seek(amp_off_t offset) noexcept
		{
			return amp_err_trace((*file_handle)->calculate_seek(buf_position, offset));
		}

		amp_err_t* close() noexcept
		{
			auto r = file_handle;
			file_handle = nullptr;

			if (r)
				return amp_err_trace((*r)->explicit_destroy());

			return AMP_NO_ERROR;
		}

		amp_err_t* flush(bool force_to_disk) noexcept
		{
			return amp_err_trace((*file_handle)->flush(force_to_disk));
		}

		amp_file_t* duplicate(amp_pool_t* pool)
		{
			return AMP_POOL_NEW(amp_file, pool, file_handle, pool);
		}

		amp_file_handle_t* get_handle() const noexcept
		{
			return file_handle;
		}
	};

	inline amp_err_t*
		amp_file_read(ptrdiff_t* bytes_read, amp_file_t* file, span<char> buffer)
	{
		return (*file)->read(bytes_read, buffer);
	}


}

AMP__PUBLIC_ACCESSOR_INPLEMENT(amp_file)
AMP__PUBLIC_ACCESSOR_INPLEMENT(amp_file_handle)
