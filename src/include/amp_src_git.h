#pragma once
#include "amp_types.h"
#include "amp_buckets.h"

AMP_C__START

enum amp_git_oid_type_t
{
	amp_git_oid_none = 0,
	amp_git_oid_sha1 = 1,
	amp_git_oid_sha256 = 2
};

enum amp_git_object_type_t
{
	amp_git_object_none = 0, // Reserved. Unused

	// These types are valid objects
	amp_git_object_commit = 1,
	amp_git_object_tree = 2,
	amp_git_object_blob = 3,
	amp_git_object_tag = 4,

	// These types are in pack files, but not real objects
	amp_git_delta_ofs = 6,
	amp_git_delta_ref = 7
};

typedef struct amp_git_oid_t
{
	amp_git_oid_type_t type;
	char bytes[32]; // long enough for sha256
} amp_git_oid_t;

AMP_DECLARE(const char*)
amp_src_git_type_name(amp_git_object_type_t type);

AMP_DECLARE(const char*)
amp_src_git_create_header(
	amp_git_object_type_t type,
	amp_off_t size,
	amp_pool_t* result_pool);

AMP_C__END