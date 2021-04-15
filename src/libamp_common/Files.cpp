#ifndef _WIN32
#include <unistd.h>
#include <sys/stat.h>
#endif

#include "amp_files.hpp"

using namespace amp;

AMP_DECLARE(amp_err_t*)
amp_file_open(
	amp_file_t** file,
	const char* path,
	int flags,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool)
{
	return amp_err_trace(
		amp_file_open_ex(
			file, path, flags, amp_fopen_share_default,
			result_pool, scratch_pool));
}

AMP_DECLARE(amp_err_t*)
amp_file_open_ex(
	amp_file_t** file,
	const char* path,
	int flags,
	int share_flags,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool)
{
	amp_file_handle_t* result;
#ifdef _WIN32
	wchar_t* wpath;
	HANDLE fh;
	DWORD dwDesiredAccess = 0;
	DWORD dwShareMode = FILE_SHARE_READ | FILE_SHARE_DELETE;
	DWORD dwCreateDisposition = 0;
	DWORD dwFlags = 0;

	AMP_ERR(amp_utf8_to_wchar(&wpath, path, scratch_pool));
	AMP_UNUSED(share_flags); // TODO

	if (flags & amp_fopen_read)
	{
		dwDesiredAccess |= GENERIC_READ;
	}
	if (flags & amp_fopen_write)
	{
		dwDesiredAccess |= GENERIC_WRITE;
	}

	if (flags & amp_fopen_create)
	{
		if (flags & amp_fopen_excl)
		{
			/* only create new if file does not already exist */
			dwCreateDisposition = CREATE_NEW;
		}
		else if (flags & amp_fopen_truncate)
		{
			/* truncate existing file or create new */
			dwCreateDisposition = CREATE_ALWAYS;
		}
		else
		{
			/* open existing but create if necessary */
			dwCreateDisposition = OPEN_ALWAYS;
		}
	}
	else if (flags & amp_fopen_truncate)
	{
		/* only truncate if file already exists */
		dwCreateDisposition = TRUNCATE_EXISTING;
	}
	else
	{
		/* only open if file already exists */
		dwCreateDisposition = OPEN_EXISTING;
	}

	if ((flags & amp_fopen_excl) && !(flags & amp_fopen_create)) {
		return amp_err_create(AMP_EACCES, nullptr, "Invalid flag combination");
	}

	fh = CreateFileW(wpath, dwDesiredAccess, dwShareMode, NULL, dwCreateDisposition, dwFlags, nullptr);
	if (fh == INVALID_HANDLE_VALUE)
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	result = new amp_file_handle(fh);
#else
	result = nullptr;
#endif
	AMP_ASSERT(result);

	*file = AMP_POOL_NEW(amp_file, result_pool, result, result_pool);
	return AMP_NO_ERROR;
}


amp_err_t*
amp_file_read(
	size_t* bytes_read,
	amp_file_t* file,
	void* buffer,
	size_t buffer_size)
{
	ptrdiff_t bytesRead;
	
	AMP_ERR(static_cast<amp_file*>(file)->read(&bytesRead, amp::span<char>(reinterpret_cast<char*>(buffer), buffer_size)));

	*bytes_read = bytesRead;

	return AMP_NO_ERROR;
}

amp_err_t*
amp_file_write(
	amp_file_t* file,
	const char* buffer,
	size_t bytes)
{
	return amp_err_trace(
		static_cast<amp_file*>(file)->write(amp_span(buffer, bytes)));
}

amp_err_t*
amp_file_seek(
	amp_file_t* file,
	amp_off_t offset)
{
	return amp_err_trace(
		static_cast<amp_file*>(file)->seek(offset));
}

amp_err_t*
amp_file_get_size(
	amp_off_t* size,
	amp_file_t* file)
{
	return amp_err_trace(
		static_cast<amp_file*>(file)->get_current_size(size));
}

amp_off_t
amp_file_get_position(
	amp_file_t* file)
{
	return static_cast<amp_file*>(file)->get_current_position();
}

amp_err_t* amp_file_close(amp_file_t *file)
{
	return amp_err_trace(
		static_cast<amp_file*>(file)->close());
}



amp_file_handle* amp_file_handle::add_ref()
{
	user_count++;
	return this;
}

void amp_file_handle::destroy()
{
	amp_err_clear(explicit_destroy());
}

amp_err_t *amp_file_handle::explicit_destroy()
{
	if (--user_count)
		return AMP_NO_ERROR;

	amp_err_t* err = AMP_NO_ERROR;
#ifdef _WIN32
	if (!CloseHandle(file_handle))
		err = amp_err_create(amp_err_get_os(), nullptr, nullptr);

	file_handle = INVALID_HANDLE_VALUE;
#else
	if (close(file_descriptor))
		err = amp_err_create(amp_err_get_os(), nullptr, nullptr);

	file_descriptor = -1;
#endif

	return err;
}

amp_err_t*
amp_file_handle::read(ptrdiff_t* bytes_read, amp_off_t offset, span<char> buffer)
{
#ifdef _WIN32
	OVERLAPPED overlapped = {};
	DWORD bytesRead;
	DWORD requested;

	overlapped.Offset = (DWORD)offset;
	overlapped.OffsetHigh = (DWORD)(offset >> 32);

	if (buffer.size() > MAXDWORD)
		requested = MAXDWORD;
	else
		requested = buffer.size();

	if (!ReadFile(file_handle, buffer.data(), requested, &bytesRead, &overlapped))
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	*bytes_read = bytesRead;
	return AMP_NO_ERROR;
#else
	ssize_t bytesRead = pread(file_descriptor, buffer.data(), buffer.size(), offset);

	if (bytes_read < 0)
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	*bytes_read = bytesRead;
	return AMP_SUCCESS;
#endif
}

amp_err_t *
amp_file_handle::get_current_size(amp_off_t *size)
{
#ifdef _WIN32
	LARGE_INTEGER r;
	if (!GetFileSizeEx(file_handle, &r))
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);
		
	*size = r.QuadPart;

	return AMP_NO_ERROR;
#else
	struct stat info;

	if (fstat(file_descriptor, &info) != 0)
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	size = info.st_size;

	return AMP_NO_ERROR;
#endif
}

amp_err_t*
amp_file_handle::write(amp_off_t offset, amp_span buffer)
{
#ifdef _WIN32
	OVERLAPPED overlapped = {};
	DWORD bytesWritten;

	AMP_ASSERT(buffer.size() < MAXDWORD);

	overlapped.Offset = (DWORD)offset;
	overlapped.OffsetHigh = (DWORD)(offset >> 32);

	if (!WriteFile(file_handle, buffer.data(), buffer.size(), &bytesWritten, &overlapped))
		return amp_err_create(amp_err_get_os(), nullptr, nullptr); // TODO: Better error
	else if (bytesWritten != (DWORD)buffer.size())
		return amp_err_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	return AMP_NO_ERROR;
#else
	ssize_t bytesWritten;

	bytesWritten = pwrite(file_descriptor, buffer.data(), buffer.size(), offset);

	return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error
	else if (bytesWritten != buffer.size())
	return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	return AMP_NO_ERROR;
#endif
}
amp_err_t* 
amp_file_handle::truncate(amp_off_t offset)
{
#ifdef _WIN32
	LARGE_INTEGER pos;
	pos.QuadPart = offset;

	if (!SetFilePointerEx(file_handle, pos, nullptr, FILE_BEGIN))
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	if (!SetEndOfFile(file_handle))
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	return AMP_NO_ERROR;
#else
	if (ftruncate(file_descriptor, offset) != 0)
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	return AMP_NO_ERROR;
#endif
}

amp_err_t* 
amp_file_handle::flush(bool force_to_disk)
{
#ifdef _WIN32
	if (force_to_disk)
	{
		if (!FlushFileBuffers(file_handle))
			return amp_err_create(amp_err_get_os(), nullptr, nullptr);
	}
	else
	{
		// Everything is already in the OS layer. We don't cache here internally
	}
#else
	if (force_to_disk)
	{
		// TODO
		// Need some ioctls. See sqlite / Subversion
	}
	else
	{
		// TODO
		// fflush like
	}
#endif
	return AMP_NO_ERROR;
}