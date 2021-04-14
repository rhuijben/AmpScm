#ifndef _WIN32
#include <unistd.h>
#include <sys/stat.h>
#endif

#include "amp_files.hpp"

using namespace amp;

AMP_DECLARE(amp_error_t*)
amp_file_open(
	amp_file_t** file,
	const char *path,
	int flags,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool)
{
	return amp_error_trace(
		amp_file_open_ex(
			file, path, flags, amp_fopen_share_default,
			result_pool, scratch_pool));
}

AMP_DECLARE(amp_error_t*)
amp_file_open_ex(
	amp_file_t** file,
	const char *path,
	int flags,
	int share_flags,
	amp_pool_t* result_pool,
	amp_pool_t* scratch_pool)
{
	amp_file_handle_t *result;
#ifdef _WIN32
	wchar_t* wpath;
	HANDLE fh;
	DWORD access = 0;
	DWORD share = FILE_SHARE_READ;
	DWORD disposition = 0;
	DWORD dwFlags = 0;

	AMP_ERR(amp_utf8_to_wchar(&wpath, path, scratch_pool));

	fh = CreateFileW(wpath, access, share, NULL, disposition, dwFlags, nullptr);
	if (fh == INVALID_HANDLE_VALUE)
		return amp_error_create(99, nullptr, "CreateFile failed");

	result = new amp_file_handle(fh);
#else
	result = nullptr;
#endif
	AMP_ASSERT(result);

	*file = AMP_POOL_NEW(amp_file, result_pool, result, result_pool);
	return AMP_NO_ERROR;
}

amp_file_handle* amp_file_handle::add_ref()
{
	user_count++;
	return this;
}

void amp_file_handle::destroy()
{
#ifdef _WIN32
	if (user_count--)
		return;

	CloseHandle(file_handle);
	file_handle = INVALID_HANDLE_VALUE;
#else
	close(file_descriptor);
	file_descriptor = -1;
#endif
	delete this;
}

amp_error_t*
amp_file_handle::read(amp_off_t* bytes_read, amp_off_t offset, span<char> buffer, ptrdiff_t requested)
{
#ifdef _WIN32
	OVERLAPPED overlapped = {};
	DWORD bytesRead;

	overlapped.Offset = (DWORD)offset;
	overlapped.OffsetHigh = (DWORD)(offset >> 32);

	if (!ReadFile(file_handle, buffer.data(), buffer.size(), &bytesRead, &overlapped))
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	*bytes_read = bytesRead;
	return AMP_SUCCESS;
#else
	ssize_t bytesRead = pread(file_descriptor, buffer.data(), buffer.size(), offset);

	if (bytes_read < 0)
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	*bytes_read = bytesRead;
	return AMP_SUCCESS;
#endif
}

amp_off_t
amp_file_handle::get_current_length()
{
#ifdef _WIN32
	LARGE_INTEGER r;
	if (GetFileSizeEx(file_handle, &r))
		return r.QuadPart;

	return -1;
#else
	struct stat info;

	if (fstat(file_descriptor, &info) == 0)
		return info.st_size;

	return -1;
#endif
}

amp_error_t*
amp_file_handle::write(amp_off_t offset, amp_span buffer)
{
#ifdef _WIN32
	OVERLAPPED overlapped = {};
	DWORD bytesWritten;

	overlapped.Offset = (DWORD)offset;
	overlapped.OffsetHigh = (DWORD)(offset >> 32);

	if (!WriteFile(file_handle, buffer.data(), buffer.size(), &bytesWritten, &overlapped))
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error
	else if (bytesWritten != buffer.size())
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	return AMP_SUCCESS;
#else
	ssize_t bytesWritten;

	bytesWritten = pwrite(file_descriptor, buffer.data(), buffer.size(), offset);

	return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error
	else if (bytesWritten != buffer.size())
	return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	return AMP_SUCCESS;
#endif
}
amp_error_t* amp_file_handle::truncate(amp_off_t offset)
{
#ifdef _WIN32
	LARGE_INTEGER pos;
	pos.QuadPart = offset;

	if (!SetFilePointerEx(file_handle, pos, nullptr, FILE_BEGIN))
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	if (!SetEndOfFile(file_handle))
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	return AMP_SUCCESS;
#else
	if (ftruncate(file_descriptor, offset) != 0)
		return amp_error_create(AMP_ERR_BAD_FILENAME, nullptr, nullptr); // TODO: Better error

	return AMP_SUCCESS;
#endif
}
