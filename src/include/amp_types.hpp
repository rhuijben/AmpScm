#pragma once

#include "amp_types.h"

namespace amp {

	class amp_destroyable
	{
	public:
		virtual void destroy(amp_pool_t* scratch_pool) {}
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

	public:
		virtual void destroy(amp_pool_t* scratch_pool) {}

		amp_pool_t* get_pool() const
		{
			return m_pool;
		}
	};


}
