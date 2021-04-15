#include <new>
#include <malloc.h>
#include <stdio.h>

#include <Windows.h>
#include "amp_pools.hpp"
#include "amp_apr.hpp"

using namespace amp;

static int
cleanup_subpool(void* data)
{
	amp_pool_destroy(reinterpret_cast<amp_pool_t*>(data));
	return 0;
}

amp_pool_t* amp_pool_create(amp_pool_t* pool)
{
	auto allocator = pool ? amp_pool_get_allocator(pool) : amp_allocator_create();
	amp_pool* new_pool;

	// Allocate pool directly in allocator
	if (!pool)
	{
		new_pool = new (amp_allocator_alloc<amp_pool>(allocator)) amp_pool(allocator, true);
	}
	else
	{
		new_pool = AMP_POOL_NEW(amp_pool, pool, pool);		
	}

	return new_pool;
}

void amp_pool_destroy(amp_pool_t* pool)
{
	static_cast<amp_pool*>(pool)->destroy(pool);
}

amp_allocator_t* amp_pool_get_allocator(amp_pool_t* pool)
{
	return static_cast<amp_pool*>(pool)->get_allocator();
}

void*
amp_palloc(
	size_t bytes,
	amp_pool_t* pool)
{
	return static_cast<amp_pool*>(pool)->alloc(bytes);
}

void*
amp_pcalloc(
	size_t bytes,
	amp_pool_t* pool)
{
	return memset(static_cast<amp_pool*>(pool)->alloc(bytes), 0, bytes);
}

void 
amp_pool_clear(
	amp_pool_t *pool)
{
	return static_cast<amp_pool*>(pool)->clear();
}

void
amp_pool_cleanup_register(
	amp_pool_t* pool,
	const void* data,
	int (*plain_cleanup)(void*),
	int (*child_cleanup)(void*))
{
	static_cast<amp_pool*>(pool)->cleanup_register(data, plain_cleanup, child_cleanup);
}

void
amp_pool_cleanup_kill(
	amp_pool_t* pool,
	const void* data,
	int (*cleanup)(void*))
{
	static_cast<amp_pool*>(pool)->cleanup_kill(data, cleanup);
}

void
amp_pool_cleanup_run(amp_pool_t* pool)
{
	static_cast<amp_pool*>(pool)->cleanup_run();
}

void
amp_pool_cleanup_run_exec(amp_pool_t* pool)
{
	static_cast<amp_pool*>(pool)->cleanup_run_exec();
}

void *
amp_pmemdup(
	const void* src,
	size_t size,
	amp_pool_t* pool)
{
	void* v = amp_palloc(size, pool);

	memcpy(v, src, size);
	return v;
}

char *
amp_pstrdup(
	const char* from,
	amp_pool_t* pool)
{
	return (char*)amp_pmemdup(from, strlen(from) + 1, pool);
}

#define MAX_SAVED_LENGTHS 16
char *
amp_pstrcat(
	amp_pool_t* pool,
	...)
{
	char *cp, *argp, *res;
	size_t saved_lengths[MAX_SAVED_LENGTHS];
	int nargs = 0;

	/* Pass one --- find length of required string */

	size_t len = 0;
	va_list args;

	va_start(args, pool);

	while ((cp = va_arg(args, char *)) != NULL) {
		size_t cplen = strlen(cp);
		if (nargs < MAX_SAVED_LENGTHS) {
			saved_lengths[nargs++] = cplen;
		}
		len += cplen;
	}

	va_end(args);

	/* Allocate the required string */

	res = (char *) amp_palloc(len + 1, pool);
	cp = res;

	/* Pass two --- copy the argument strings into the result space */

	va_start(args, pool);

	nargs = 0;
	while ((argp = va_arg(args, char *)) != NULL) {
		if (nargs < MAX_SAVED_LENGTHS) {
			len = saved_lengths[nargs++];
		}
		else {
			len = strlen(argp);
		}

		memcpy(cp, argp, len);
		cp += len;
	}

	va_end(args);

	/* Return the result string */

	*cp = '\0';

	return res;
}

char *
amp_psprintf(
	amp_pool_t* pool,
	const char* format,
	...)
{
	va_list args;
	char* r;
	va_start(args, format);

	r = amp_pvsprintf(pool, format, args);

	va_end(args);

	return r;
}

char *
amp_pvsprintf(
	amp_pool_t* pool,
	const char* format,
	va_list args)
{
	char* r;
	int n;

	// Legacy MSVC might need, but let's assume something recent
	// n = _vscprintf(format, args);
	n = vsnprintf(NULL, 0, format, args);

	AMP_ASSERT(n >= 0);
	if (n < 0)
		return nullptr;
	
	r = amp_palloc_n<char>(n + 1, pool);

	vsnprintf(r, n + 1, format, args);

	return r;
}

// Cleanup pool with parent
amp_pool::amp_pool(amp_allocator_t* allocator, bool owns_allocator)
{
	AMP_ASSERT(allocator);
	m_allocator = allocator;
	m_parent = nullptr;
	m_first_cleanup = nullptr;
	m_first_page = nullptr;
	m_owns_allocator = owns_allocator;
}

amp_pool::amp_pool(amp_pool_t* parent_pool)
{
	AMP_ASSERT(parent_pool);
	m_parent = parent_pool;
	m_allocator = static_cast<amp_pool*>(parent_pool)->get_allocator();
	m_first_cleanup = nullptr;
	m_first_page = nullptr;
	m_owns_allocator = false;
	// Cleanup pool with parent

	amp_pool_cleanup_register(parent_pool, static_cast<amp_pool_t*>(this), cleanup_subpool, cleanup_subpool);
}

void*
amp_pool::alloc(size_t bytes)
{
	AMP_ASSERT(bytes >= 0);

	if (!m_first_page || m_first_page->data_left < bytes)
	{
		size_t alloc = max(bytes, 65536);

		alloc = (alloc + 15) & ~0xF;

		page_t* p = (page_t*)amp_allocator_alloc(sizeof(*p) + alloc, m_allocator);
		memset(p, 0, sizeof(*p));

		p->next = m_first_page;
		p->data_left = alloc;

		m_first_page = p;
	}

	AMP_ASSERT(bytes <= m_first_page->data_left);
	m_first_page->data_left -= bytes;
	return reinterpret_cast<char*>(&m_first_page[1]) + m_first_page->data_left;	
}

void 
amp_pool::destroy(amp_pool_t* pool)
{
	AMP_ASSERT(pool == this);
	clear();

	if (!m_parent)
	{
		auto allocator = m_allocator;
		bool destroy_allocator = m_owns_allocator;

		amp_allocator_free(this, allocator);

		if (destroy_allocator)
			amp_allocator_destroy(allocator);
	}
}

void 
amp_pool::clear()
{
	cleanup_run();

	page_t* page = m_first_page;
	m_first_page = nullptr;
	while (page)
	{
		page_t* del = page;
		page = del->next;

		amp_allocator_free(del, m_allocator);
	}	
}

void 
amp_pool::cleanup_run()
{
	cleanup_t* c = m_first_cleanup;
	m_first_cleanup = nullptr;

	while (c)
	{
		if (c->plain_cleanup)
			c->plain_cleanup(const_cast<void*>(c->data));
		c = c->next;
	}
}

void 
amp_pool::cleanup_run_exec()
{
	cleanup_t* c = m_first_cleanup;
	m_first_cleanup = nullptr;

	while (c)
	{
		if (c->exec_cleanup)
			c->exec_cleanup(const_cast<void*>(c->data));
		c = c->next;
	}
}

void 
amp_pool::cleanup_register(
	const void* data,
	amp_pool_cleanup_func_t plain_cleanup,
	amp_pool_cleanup_func_t exec_cleanup)
{
	cleanup_t* c = amp_pcalloc<cleanup_t>(this);

	c->next = m_first_cleanup;
	m_first_cleanup = c;

	c->data = data;
	c->plain_cleanup = plain_cleanup;
	c->exec_cleanup = exec_cleanup;
}

void
amp_pool::cleanup_kill(const void* data,
					   amp_pool_cleanup_func_t plain_cleanup)
{
	cleanup_t* c = m_first_cleanup;
	m_first_cleanup = nullptr;

	while (c)
	{
		if (c->plain_cleanup == plain_cleanup && c->data == data)
		{
			c->plain_cleanup = nullptr;
			c->exec_cleanup = nullptr;
			c->data = nullptr;
			break;
		}

		c = c->next;
	}
}

amp_err_t*
amp_utf8_to_wchar(
	wchar_t** dest,
	const char* src,
	amp_pool_t* result_pool)
{
#ifdef _WIN32
	int n = strlen(src);

	if (n == 0)
	{
		*dest = amp_pcalloc_n<wchar_t>(1, result_pool);
		return nullptr;
	}

	int required = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, src, n, nullptr, 0);

	if (required <= 0)
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	wchar_t* buffer = amp_palloc_n<wchar_t>(required + 1, result_pool);
	buffer[required] = 0;

	if(MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, src, n, buffer, required+1) != required)
		return amp_err_create(amp_err_get_os(), nullptr, nullptr);

	*dest = buffer;

	return AMP_NO_ERROR;
#else
	return amp_error_create(AMP_ERR_BAD_CHECKSUM_KIND, nullptr, "Not implemented yet");
#endif
}

char *
amp_wchar_to_utf8(
	const wchar_t *src,
	amp_pool_t* result_pool)
{
#ifdef _WIN32
	int n = wcslen(src);

	if (n == 0)
	{
		return amp_pcalloc_n<char>(1, result_pool);
	}

	int required = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, src, n, nullptr, 0, nullptr, nullptr);

	if (required <= 0)
		return amp_pstrdup("<?>", result_pool);

	char* buffer = amp_palloc_n<char>(required + 1, result_pool);
	buffer[required] = 0;

	if(WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, src, n, buffer, required, nullptr, nullptr) != required)
		return amp_pstrdup("<?>", result_pool);

	return buffer;
#else
	return amp_error_create(AMP_ERR_BAD_CHECKSUM_KIND, nullptr, "Not implemented yet");
#endif
}