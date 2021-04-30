#pragma once

namespace amp
{
	template<typename T>
	void amp_linkedlist_append(T*& first, T*& last, T* new_item)
	{
		new_item->prev = last;
		new_item->next = nullptr;

		if (last)
			last->next = new_item;
		else
			first = new_item;
		last = new_item;
	}

	template<typename T>
	void amp_linkedlist_prepend(T*& first, T*& last, T* new_item)
	{
		new_item->prev = nullptr;
		new_item->next = first;

		if (first)
			first->prev = new_item;
		else
			last = new_item;

		first = new_item;
	}

	template<typename T>
	void amp_linkedlist_remove(T*& first, T*& last, T* old_item)
	{
		if (old_item->prev)
			old_item->prev->next = old_item->next;
		else
			first = old_item->next;

		if (old_item->next)
			old_item->next->prev = old_item->prev;
		else
			last = old_item->prev;
	}


}