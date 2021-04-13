#include "amp_types.hpp"
#pragma once

#ifdef _WIN32
#include <winnt.h>
#include <atomic>
#endif

#include "amp_files.h"
#include "amp_span.hpp"


struct amp_file_handle_t
{
protected:
#ifdef _WIN32
	HANDLE file_handle;
	std::atomic_int32_t user_count;
#else
	int file_descriptor;
	amp_off_t cur_position;
#endif	
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
			cur_position = 0;
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
}