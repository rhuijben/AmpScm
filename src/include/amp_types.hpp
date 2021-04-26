#pragma once

#include "amp_types.h"

#ifdef _MSC_VER
#pragma warning(disable: 26812)
#endif

namespace amp {

	class amp_destroyable
	{
	public:
		virtual void destroy(amp_pool_t* scratch_pool) 
		{
			AMP_UNUSED(scratch_pool);
		}
	};

	class amp_pool_managed : amp_destroyable
	{
	private:
		amp_pool_t* m_pool;


	protected:
		amp_pool_managed(amp_pool_t* pool)
		{
			m_pool = pool;
		}

		void destroy_with_pool();
		void kill_cleanup();

	public:
		virtual void destroy(amp_pool_t* scratch_pool) 
		{
			AMP_UNUSED(scratch_pool);
		}

		amp_pool_t* get_pool() const
		{
			return m_pool;
		}
	};
}

#define AMP__PUBLIC_ACCESSOR_DECLARE(type)				\
	public:												\
		constexpr class amp::type * operator->() noexcept;

#define AMP__PUBLIC_ACCESSOR_INPLEMENT(type)	\
	inline constexpr amp::type * type ## _t::operator->() noexcept { return static_cast<amp::type*>(this); }

// Same list as amp_types.h does for .h files
#include "amp_error.hpp"
#include "amp_pools.hpp"
