#pragma once

#include "amp_types.hpp"

namespace amp
{
	class amp_allocator;
	class amp_pool;
}

struct amp_allocator_t
{
	struct allocation_t
	{
		allocation_t* next;
		allocation_t* prev;
		size_t size;
	};
protected:
	allocation_t* first, * last;
	amp_allocator_alloc_func_t m_alloc_func;
	amp_allocator_free_func_t m_free_func;
	amp_allocator_realloc_func_t m_realloc_func;
	amp_allocator_abort_func_t m_abort_func;

	AMP__PUBLIC_ACCESSOR_DECLARE(amp_allocator)
};

struct amp_pool_t
{
	struct page_t
	{
		page_t* next;
		size_t data_left;
	};

	struct cleanup_t
	{
		cleanup_t* next;
		const void* data;
		amp_pool_cleanup_func_t plain_cleanup;
		amp_pool_cleanup_func_t exec_cleanup;
	};

protected:
	amp_allocator_t* m_allocator;
	amp_pool_t* m_parent;

	page_t* m_first_page;
	cleanup_t* m_first_cleanup;
	bool m_owns_allocator;

	AMP__PUBLIC_ACCESSOR_DECLARE(amp_pool)
};

namespace amp {
	class amp_allocator : public amp_allocator_t
	{
	public:
		amp_allocator(amp_allocator_alloc_func_t alloc_func,
						amp_allocator_free_func_t free_func)
			: amp_allocator(alloc_func, free_func, nullptr, nullptr)
		{

		}

		amp_allocator(
			amp_allocator_alloc_func_t alloc_func,
			amp_allocator_free_func_t free_func,
			amp_allocator_realloc_func_t realloc_func,
			amp_allocator_abort_func_t abort_func)
		{
			AMP_ASSERT(alloc_func && free_func);

			m_alloc_func = alloc_func;
			m_free_func = free_func;
			m_realloc_func = realloc_func;
			m_abort_func = abort_func;
			first = last = nullptr;
		}

	public:
		void* alloc(size_t size);
		void free(void* data);
		void* realloc(void* data, size_t new_size);
		void destroy();
	};


	class amp_pool : public amp_pool_t, public amp_destroyable
	{
	public:
		amp_pool(amp_allocator_t* allocator, bool owns_allocator);
		amp_pool(amp_pool_t* parent_pool);

	public:
		void* alloc(size_t bytes);
		void clear();
		virtual void destroy(amp_pool_t* pool) override;

	public:
		void cleanup_run();
		void cleanup_run_exec();
		void cleanup_register(
			const void* data,
			amp_pool_cleanup_func_t plain_cleanup,
			amp_pool_cleanup_func_t exec_cleanup);
		void cleanup_kill(
			const void* data,
			amp_pool_cleanup_func_t plain_cleanup);

	public:
		constexpr amp_allocator_t* get_allocator() const
		{
			return m_allocator;
		}
	};


	template<typename T>
	inline T* amp_palloc(amp_pool_t* pool)
	{
		return (T*)amp_palloc(sizeof(T), pool);
	}

	template<typename T>
	inline T*
		amp_palloc_n(
			size_t count,
			amp_pool_t* pool)
	{
		return (T*)amp_palloc(sizeof(T) * count, pool);
	}

	template<typename T>
	inline T*
		amp_pcalloc_n(
			size_t count,
			amp_pool_t* pool)
	{
		return (T*)memset(amp_palloc(sizeof(T) * count, pool), 0, sizeof(T) * count);
	}


	template<typename T>
	inline T* amp_pcalloc(amp_pool_t* pool)
	{
		return (T*)amp_pcalloc(sizeof(T), pool);
	}

	template<typename T>
	inline T* amp_allocator_alloc(amp_allocator_t* allocator)
	{
		return (T*)amp_allocator_alloc(sizeof(T), allocator);
	}

	template<typename T>
	inline T* amp_allocator_alloc_n(size_t count, amp_allocator_t* allocator)
	{
		return (T*)amp_allocator_alloc(sizeof(T) * count, allocator);
	}


#define AMP_POOL_NEW(type, pool, ...) new (amp::amp_palloc<type>(pool)) type(__VA_ARGS__)
#define AMP_ALLOCATOR_NEW(type, allocator, ...) new (amp::amp_allocator_alloc<type>(allocator)) type(__VA_ARGS__)	
}

AMP__PUBLIC_ACCESSOR_INPLEMENT(amp_allocator)
AMP__PUBLIC_ACCESSOR_INPLEMENT(amp_pool)