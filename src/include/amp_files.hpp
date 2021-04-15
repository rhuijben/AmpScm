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


struct amp_file_handle_t
{
protected:
	std::atomic_int32_t user_count;

#ifdef _WIN32
	HANDLE file_handle;	
#else
	int file_descriptor;
#endif	
};

struct amp_file_t
{
protected:
	amp_file_handle_t* file_handle;
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
		amp_err_t* read(ptrdiff_t* bytes_read, amp_off_t offset, span<char> buffer);
		amp_err_t* get_current_size(amp_off_t *size);
		amp_err_t* write(amp_off_t offset, amp_span data);
		amp_err_t* truncate(amp_off_t offset);
		amp_err_t* flush(bool force_to_disk);
	};

	class amp_file : public amp_file_t, amp_pool_managed
	{
	private:
		amp_off_t position;
	public:
		amp_file(amp_file_handle_t* handle, amp_pool_t *pool)
			: amp_pool_managed(pool)
		{
			file_handle = static_cast<amp_file_handle*>(handle)->add_ref();
			position = 0;

			destroy_with_pool();
		}

		~amp_file()
		{
			destroy(nullptr);
		}

		virtual void destroy(amp_pool_t *) override
		{
			auto r = static_cast<amp_file_handle*>(file_handle);
			file_handle = nullptr;

			if (r)
				r->destroy();
		}

	public:
		amp_err_t* read(ptrdiff_t* bytes_read, span<char> buffer)
		{
			ptrdiff_t b_read;

			AMP_ERR(static_cast<amp_file_handle*>(file_handle)->read(&b_read, position, buffer));

			position += b_read;
			*bytes_read = b_read;
			return AMP_NO_ERROR;
		}

		amp_err_t* get_current_size(amp_off_t *size)
		{
			return amp_err_trace(
				static_cast<amp_file_handle*>(file_handle)->get_current_size(size)
				);
		}

		amp_off_t get_current_position()
		{
			return position;
		}

		amp_err_t* write(amp_span buffer)
		{
			AMP_ERR(static_cast<amp_file_handle*>(file_handle)->write(position, buffer));

			position += buffer.size_bytes();
			return AMP_NO_ERROR;
		}

		amp_err_t* truncate()
		{
			return amp_err_trace(static_cast<amp_file_handle*>(file_handle)->truncate(position));
		}

		amp_err_t* seek(amp_off_t offset)
		{
			if (offset == 0)
				position = 0;

			amp_off_t sz;
			AMP_ERR(get_current_size(&sz));

			if (offset <= sz)
				position = offset;
			else
				return amp_err_create(AMP_BADARG, nullptr, "Offset behind file");

			return AMP_NO_ERROR;
		}

		amp_err_t* close()
		{
			auto r = static_cast<amp_file_handle*>(file_handle);
			file_handle = nullptr;

			if (r)
				return amp_err_trace(r->explicit_destroy());

			return AMP_NO_ERROR;
		}

		amp_err_t* flush(bool force_to_disk)
		{
			return amp_err_trace(static_cast<amp_file_handle*>(file_handle)->flush(force_to_disk));
		}
	};
}