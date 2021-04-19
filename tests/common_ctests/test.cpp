#include "pch.h"

#include <windows.h>

#include "amp_pools.hpp"
#include "amp_buckets.hpp"
#include "amp_files.hpp"

using namespace amp;

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
	const char* temp_path;

	virtual void SetUp() override
	{
		pool = amp_pool_create(nullptr);
	}

	virtual void TearDown() override
	{
		amp_pool_destroy(pool);
	}

	const char* get_temp_path()
	{
		char* temp_path = amp_pcalloc_n<char>(200, pool);

		if (GetTempPathA(199, temp_path) != 0)
		{
			return temp_path;
		}
		else
			return nullptr;
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
			amp_palloc(j * 400 * (1 + i), subpool);

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
	char* tf = amp_pstrcat(pool, get_temp_path(), "SimpleFile.txt", AMP_VA_NULL);

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

TEST_F(BasicTest, SimpleBucketRead)
{
	const char* nlTest = "New line\nCarriage return\rWindows style\r\nBadWay\n\rDoubleOut\n\nDoubleOther\r\rCarriage return before Windows\r\r\nAnd\r\r\n\n";
	char* tf = amp_pstrcat(pool, get_temp_path(), "SimpleFile.txt", AMP_VA_NULL);
	amp_file_t* file;

	TEST_ERR(amp_file_open(&file, tf, amp_fopen_create | amp_fopen_write | amp_fopen_truncate | amp_fopen_read, pool, pool));
	for (int i = 0; i < 1200; i++)
		TEST_ERR(amp_file_write(file, nlTest, strlen(nlTest)));
	TEST_ERR(amp_file_close(file));

	TEST_ERR(amp_file_open(&file, tf, amp_fopen_read | amp_fopen_del_on_close, pool, pool));

	amp_bucket_t* bk = amp_bucket_file_create(file, amp_pool_get_allocator(pool), pool);

	amp_span data;
	amp_newline_t which;
	
	TEST_ERR((*bk)->read_until_eol(&data, &which, amp_newline_any, AMP_READ_ALL_AVAIL, pool));

	ASSERT_EQ(which, amp_newline_lf);
	ASSERT_EQ(9, data.size_bytes());
	ASSERT_EQ(0, memcmp("New line\n", data.data(), 9));

	TEST_ERR((*bk)->read(&data, 125, pool));
	ASSERT_GT(data.size_bytes(), 1);

	for (int i = 0; i < 10000; i++)
	{
		TEST_ERR((*bk)->read_until_eol(&data, &which, amp_newline_any, AMP_READ_ALL_AVAIL, pool));
	}

	amp_bucket_destroy(bk, pool);
}

TEST_F(BasicTest, SimpleBucketCreateRead)
{
	amp_allocator_t* allocator = (*pool)->get_allocator();

	auto bk = amp_bucket_aggregate_create(allocator);

	amp_bucket_aggregate_append(bk, amp_bucket_simple_create("blob 26\n", 8, allocator));
	amp_bucket_aggregate_append(bk, amp_bucket_simple_create("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 26, allocator));


	amp_hash_result_t* hash_result_sha1 = nullptr;
	amp_hash_result_t* hash_result_sha256 = nullptr;
	amp_hash_result_t* hash_result_md5 = nullptr;
	TEST_ERR(
		amp_bucket_hash_create(&bk, &hash_result_sha1,
							  bk, amp_hash_algorithm_sha1, nullptr,
						      allocator, pool));
	TEST_ERR(
		amp_bucket_hash_create(&bk, &hash_result_sha256,
							   bk, amp_hash_algorithm_sha256, nullptr,
							   allocator, pool));
	TEST_ERR(
		amp_bucket_hash_create(&bk, &hash_result_md5,
							   bk, amp_hash_algorithm_md5, nullptr,
							   allocator, pool));

	ASSERT_NE(hash_result_sha1, nullptr);
	ASSERT_EQ(hash_result_sha1->hash_algorithm, amp_hash_algorithm_sha1);
	ASSERT_EQ(hash_result_sha1->bytes[0], 0);

	const char* buf;
	size_t sz;
	TEST_ERR(amp_bucket_read(&buf, &sz, bk, AMP_READ_ALL_AVAIL, pool));
	ASSERT_EQ(sz, 8);
	ASSERT_EQ(0, memcmp("blob 26\n", buf, 8));

	TEST_ERR(amp_bucket_read(&buf, &sz, bk, AMP_READ_ALL_AVAIL, pool));
	ASSERT_EQ(sz, 26);
	ASSERT_EQ(0, memcmp("ABCDEFGHIJKLMNOPQRSTUVWXYZ", buf, 26));

	auto err = amp_bucket_read(&buf, &sz, bk, AMP_READ_ALL_AVAIL, pool);
	ASSERT_TRUE(AMP_ERR_IS_EOF(err));
	ASSERT_EQ(sz, 0);
	amp_err_clear(err);

	ASSERT_NE(hash_result_sha1, nullptr);
	ASSERT_EQ(hash_result_sha1->hash_algorithm, amp_hash_algorithm_sha1);
	ASSERT_NE(hash_result_sha1->bytes[0], 0);

	amp_bucket_destroy(bk, pool);

	ASSERT_STREQ("A3376BEEFF28183E2A4EFE532F17592CDE996189", amp_hash_result_to_cstring(hash_result_sha1, true, pool));
	ASSERT_STREQ("58477859E5F1AFE259FF8234C20943B1C35B93B0B1D11483B5D882B3EBA16501", amp_hash_result_to_cstring(hash_result_sha256, true, pool));
	ASSERT_STREQ("305CA3AC86979B00226D432F70533BC2", amp_hash_result_to_cstring(hash_result_md5, true, pool));
}