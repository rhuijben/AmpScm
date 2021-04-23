#pragma once
#include "amp_src_git.h" 
#include "amp_buckets.hpp"

namespace amp
{


	class amp_bucket_git_pack_frame : public amp_bucket
	{
	private:
		amp_bucket_t* wrapped;
		amp_bucket_t* reader;
		enum class state
		{
			start,
			size_done,
			find_delta,
			body
		} state;
		amp_off_t body_size;
		amp_off_t position;
		amp_off_t frame_position;
		amp_off_t delta_position;
		amp_git_oid_t base_oid;
		amp_git_object_type_t git_type;
		int delta_count;

	public:
		amp_bucket_git_pack_frame(amp_bucket_t* from, amp_git_oid_type_t git_oid_type, amp_allocator_t* allocator);

		virtual void destroy(amp_pool_t* scratch_pool) override;
	public:
		virtual amp_err_t* read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_until_eol(
			amp_span* data,
			amp_newline_t* found,
			amp_newline_t acceptable,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* reset(
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_remaining_bytes(
			amp_off_t* remaining,
			amp_pool_t* scratch_pool) override;

		virtual amp_off_t get_position() override;

	public:
		amp_err_t* read_frame_info(
			amp_git_object_type_t* obj_type,
			int* delta_count,
			amp_pool_t* scratch_pool);
	};

	class amp_bucket_git_delta : public amp_bucket
	{
	private:
		amp_bucket_t* src;
		amp_bucket_t* base;
		amp_off_t length;
		amp_off_t position;
		char buffer[8];
		ptrdiff_t copy_offset;
		ptrdiff_t copy_size;
		enum class state
		{
			start,
			init,
			src_copy,
			base_copy,
			eof
		} state;
		ptrdiff_t p0;

	public:
		amp_bucket_git_delta(
			amp_bucket_t* delta_src,
			amp_bucket_t* delta_base,
			amp_allocator_t* allocator);

		virtual void destroy(amp_pool_t* scratch_pool) override;

	private:
		amp_err_t* advance(amp_pool_t *scratc_pool);

	public:
		virtual amp_err_t* read(
			amp_span* data,
			ptrdiff_t requested,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* peek(
			amp_span* data,
			bool no_poll,
			amp_pool_t* scratch_pool) override;

		virtual amp_err_t* read_remaining_bytes(
			amp_off_t* remaining,
			amp_pool_t* scratch_pool) override;

		virtual amp_off_t get_position() override;
	};
}