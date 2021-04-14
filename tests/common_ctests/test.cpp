#include "pch.h"

#include <windows.h>

#include "amp_pools.h"
#include "amp_buckets.hpp"
#include "amp_files.hpp"
TEST(BasicTest, PoolSetup) 
{
	auto pool = amp_pool_create(nullptr);
	auto subpool = amp_pool_create(pool);

	for (int i = 0; i < 10; i++)
	{
		for (int j = 1; j < 10; j++)
		{
			amp_palloc(j * 400 * (1+i), subpool);

			amp_pstrdup("qwerty", subpool);			
		}
		amp_pool_clear(subpool);
	}

	char* r = amp_pstrcat(subpool, "A", "B", "C", AMP_VA_NULL);
	ASSERT_STREQ(r, "ABC");

	r = amp_pprintf(subpool, "B %d", 12);

	ASSERT_STREQ(r, "B 12");

	amp_pool_destroy(pool);
}

TEST(BasicTest, SimpleFileIO)
{
	auto pool = amp_pool_create(nullptr);
	char* temp_path = amp_pcalloc_n<char>(200, pool);
	ASSERT_NE(0, GetTempPathA(199, temp_path));

	char* tf = amp_pstrcat(pool, temp_path, "/SimpleFile.txt", AMP_VA_NULL);

	amp_file_t* file;
	ASSERT_EQ(0, amp_file_open(&file, tf, amp_fopen_create | amp_fopen_write | amp_fopen_truncate, pool, pool));
	
}