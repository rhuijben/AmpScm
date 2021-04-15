#include "pch.h"

#include <windows.h>

#include "amp_pools.hpp"
#include "amp_buckets.hpp"
#include "amp_files.hpp"

#define TEST_ERR(x)			\
  do { amp_err_t *e = (x);		\
       if (e)				\
	   {					\
			auto ee = e;	\
			while (ee) { \
				fprintf(stderr, "%s\n", amp_err_message(ee)); \
				ee = ee->child; \
			} \
			amp_err_clear(e); \
			ASSERT_EQ(nullptr, e); \
	   }					\
  } while(0)

class BasicTest : public testing::Test
{
public:
	amp_pool_t* pool;

	virtual void SetUp() override
	{
		pool = amp_pool_create(nullptr);
	}

	virtual void TearDown() override
	{
		amp_pool_destroy(pool);
	}
};

TEST_F(BasicTest, PoolSetup) 
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

	r = amp_psprintf(subpool, "B %d", 12);

	ASSERT_STREQ(r, "B 12");

	amp_pool_destroy(pool);
}

TEST_F(BasicTest, SimpleFileIO)
{
	char* temp_path = amp_pcalloc_n<char>(200, pool);
	ASSERT_NE(0, GetTempPathA(199, temp_path));

	char* tf = amp_pstrcat(pool, temp_path, "SimpleFile.txt", AMP_VA_NULL);

	amp_file_t* file;

	// Test some basic file IO.
	// 
	TEST_ERR(amp_file_open(&file, tf, amp_fopen_create | amp_fopen_write | amp_fopen_truncate | amp_fopen_read, pool, pool));	
	TEST_ERR(amp_file_write(file, "New line\n", 9));

	ASSERT_EQ(9, amp_file_get_position(file));
	TEST_ERR(amp_file_seek(file, 0));

	ASSERT_EQ(0, amp_file_get_position(file));

	char buffer[128];
	size_t bytes_read;
	TEST_ERR(amp_file_read(&bytes_read, file, buffer, sizeof(buffer)));

	ASSERT_EQ(9, bytes_read);
	buffer[9] = 0;
	ASSERT_STREQ(buffer, "New line\n");

	ASSERT_EQ(9, amp_file_get_position(file));

	TEST_ERR(amp_file_close(file));
}
