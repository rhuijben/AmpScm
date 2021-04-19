#include "amp_types.hpp"

using namespace amp;

#pragma comment(lib, "bcrypt.lib")

static int pool_managed_cleanup(void* data)
{
	auto pm = reinterpret_cast<amp_pool_managed*>(data);
	
	pm->destroy(pm->get_pool());
	return 0;
}

void amp_pool_managed::destroy_with_pool()
{
	amp_pool_cleanup_register(m_pool, this, pool_managed_cleanup, pool_managed_cleanup);
}

void amp_pool_managed::kill_cleanup()
{
	amp_pool_cleanup_kill(m_pool, this, pool_managed_cleanup);
}
