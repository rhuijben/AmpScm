#include "amp_types.hpp"
#pragma once

#include <memory>
#include <atomic>
#ifdef _WIN32
#include <winnt.h>

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

	public:
		amp_error_t* read(amp_off_t* bytes_read, amp_off_t offset, span<char> buffer, ptrdiff_t requested);
		amp_off_t get_current_length();
		amp_error_t* write(amp_off_t offset, amp_span data);
		amp_error_t* truncate(amp_off_t offset);
	};

	class amp_file : public amp_file_t
	{
	private:
		amp_off_t position;
	public:
		amp_file(amp_file_handle_t* handle, amp_pool_t *pool)
		{
			file_handle = static_cast<amp_file_handle*>(file_handle)->add_ref();
			position = 0;
		}

		~amp_file()
		{
			auto r = static_cast<amp_file_handle*>(file_handle);
			file_handle = nullptr;

			if (r)
				r->destroy();
		}

	public:
		amp_error_t* read(amp_off_t* bytes_read, amp_off_t offset, span<char> buffer, ptrdiff_t requested)
		{
			amp_off_t b_read;

			AMP_ERR(static_cast<amp_file_handle*>(file_handle)->read(&b_read, position, buffer, requested));

			position += b_read;
			*bytes_read = b_read;
			return AMP_NO_ERROR;
		}

		amp_off_t get_current_length()
		{
			return static_cast<amp_file_handle*>(file_handle)->get_current_length();
		}

		amp_error_t* write(amp_span buffer)
		{
			AMP_ERR(static_cast<amp_file_handle*>(file_handle)->write(position, buffer));

			position += buffer.size_bytes();
			return AMP_NO_ERROR;
		}

		amp_error_t* truncate()
		{
			AMP_ERR(static_cast<amp_file_handle*>(file_handle)->truncate(position));
		}
	};
}