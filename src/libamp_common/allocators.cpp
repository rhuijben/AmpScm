#include <new>
#include <malloc.h>
#include <amp_pools.hpp>

using namespace amp;

amp_allocator_t*
amp_allocator_create(void)
{
	return amp_allocator_create_ex(malloc, free);
}

amp_allocator_t*
amp_allocator_create_ex(
	amp_allocator_alloc_func_t alloc_func,
	amp_allocator_free_func_t free_func)
{
	auto p = new (alloc_func(sizeof(amp_allocator))) amp_allocator(alloc_func, free_func);


	return p;
}

void*
amp_allocator_alloc(
	size_t size,
	amp_allocator_t* allocator)
{
	AMP_ASSERT(size > 0);
	return static_cast<amp_allocator*>(allocator)->alloc(size);
}

void
amp_allocator_free(
	void* data,
	amp_allocator_t* allocator)
{
	return static_cast<amp_allocator*>(allocator)->free(data);
}

void
amp_allocator_destroy(
	amp_allocator_t* allocator)
{
	return static_cast<amp_allocator*>(allocator)->destroy();
}


void*
amp_allocator::alloc(size_t size)
{
	AMP_ASSERT(size > 0 && size < (size_t)(-65536));

	size += sizeof(allocation_t);

	allocation_t* t = (allocation_t*)(*m_alloc_func)(size);

	t->prev = last;
	t->next = nullptr;
	if (last)
		last->next = t;
	else
		first = t;	

	return &t[1];
}

void
amp_allocator::free(void* data)
{
	allocation_t* t = reinterpret_cast<allocation_t*>(data) -1;

	if (t->prev)
		t->prev->next = t->next;
	else
		first = t->next;

	if (t->next)
		t->next->prev = t->prev;
	else
		last = t->prev;

	(*m_free_func)(t);
}

void* 
amp_allocator::realloc(void* data, size_t new_size)
{
	allocation_t* t = reinterpret_cast<allocation_t*>(data) -1;

	AMP_ASSERT(new_size > 0 && new_size < (size_t)(-1024*1024));

	size_t size = new_size + sizeof(allocation_t);

	void* nw;
	if (m_realloc_func)
	{
		nw = m_realloc_func(data, new_size, t->size + sizeof(allocation_t));

		if (!nw)
			return nullptr;

		t = reinterpret_cast<allocation_t*>(nw);
	}
	else
	{
		void* nw = (*m_alloc_func)(new_size);

		if (!nw)
			return nullptr;

		memcpy(nw, t, size);
		t = reinterpret_cast<allocation_t*>(nw);

		(*m_free_func)(data);
	}

	t->size = new_size;

	if (t->prev)
		t->prev->next = t;
	else
		first = t;

	if (t->next)
		t->next->prev = t;
	else
		last = t;

	return &t[1];
}

void
amp_allocator::destroy()
{
	AMP_ASSERT(!first && !last);

	while (first)
		this->free(&first[1]);

	(m_free_func)(this);
}
