#pragma once

namespace amp
{
	template<typename T = char> class span
	{
	private:
		T* m_data;
		ptrdiff_t m_size;

	public:
		typedef T element_type;
		typedef ptrdiff_t size_type;
		typedef ptrdiff_t difference_type;
		typedef T* pointer;
		typedef const T* const_pointer;
		typedef T& reference;
		typedef const T& const_reference;
		typedef T* iterator;

		span()
		{
			m_data = nullptr;
			m_size = 0;
		}

		span(T* data, ptrdiff_t size)
		{
			AMP_ASSERT(data && size >= 0);
			m_data = data;
			m_size = size;
		}

		span(const span& from) noexcept
		{
			m_data = from.m_data;
			m_size = from.m_size;
		}

		constexpr ptrdiff_t size() const noexcept
		{
			return m_size;
		}

		constexpr ptrdiff_t size_bytes() const noexcept
		{
			return m_size * sizeof(T);
		}

		constexpr bool empty() const noexcept
		{
			return (m_size == 0);
		}

		constexpr reference front() const noexcept
		{
			return m_data;
		}

		constexpr reference back() const
		{
			return m_data + m_size - 1;
		}

	public:
		constexpr span first(size_type size) const
		{
			AMP_ASSERT(size >= 0);

			if (size < m_size)
				return span(m_data, size);
			else
				return span(m_data, m_size);
		}

		constexpr span subspan(size_type offset, size_type count) const
		{
			AMP_ASSERT(offset >= 0 && offset <= m_size && count >= 0);

			if (offset + count > m_size)
				return span(m_data + offset, m_size - offset);
			else
				return span(m_data + offset, count);
		}

		constexpr span subspan(size_type offset) const
		{
			AMP_ASSERT(offset >= 0 && offset <= m_size);

			return span(m_data + offset, m_size - offset);
		}

		constexpr span last(size_type size) const
		{
			AMP_ASSERT(size >= 0);

			if (size >= m_size)
				return span(m_data, m_size);
			else
				return span(m_data + m_size - size, size);
		}

	public:
		constexpr iterator begin() const noexcept
		{
			return m_data;
		}
		constexpr iterator end() const noexcept
		{
			return m_data + m_size;
		}

		constexpr iterator rbegin() const noexcept
		{
			return m_data + m_size - 1;
		}

		constexpr iterator rend() const noexcept
		{
			return m_data - 1;
		}

		constexpr iterator data() const noexcept
		{
			return m_data;
		}

		const reference operator[](size_type idx) const
		{
			AMP_ASSERT(idx >= 0 && idx < m_size);

			return m_data[idx];
		}

	public:
		constexpr operator span<const T>() const noexcept
		{
			return span<const T>(data(), size());
		}
	};

	typedef span<const char> amp_span;
}
