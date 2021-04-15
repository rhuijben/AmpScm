#include <stdarg.h>
#include "common.hpp"
#include "amp_pools.hpp"
#include <algorithm>
#include <Windows.h>

const char*
amp_err_symbolic_name(amp_status_t statcode);


/*
 * Undefine the helpers for creating errors.
 *
 * *NOTE*: Any use of these functions in any other function may need
 * to call amp_err__locate() because the macro that would otherwise
 * do this is being undefined and the filename and line number will
 * not be properly set in the static error_file and error_line
 * variables.
 */
#undef amp_err_create
#undef amp_err_createf
#undef amp_err_quick_wrap
#undef amp_err_quick_wrapf
#undef amp_err_wrap_status

#ifndef AMP_ERR__TRACING
#define AMP_ERR__TRACING
#endif

thread_local static const char* volatile last_error_file = nullptr;
thread_local volatile static int last_error_line = 0;
thread_local static const char* volatile last_error_function = nullptr;

/*
 * Undefine the helpers for creating errors.
 *
 * *NOTE*: Any use of these functions in any other function may need
 * to call amp_err__locate() because the macro that would otherwise
 * do this is being undefined and the filename and line number will
 * not be properly set in the static error_file and error_line
 * variables.
 */
#undef amp_err_create
#undef amp_err_createf
#undef amp_err_quick_wrap
#undef amp_err_quick_wrapf
#undef amp_err_wrap_status

 /* Note: Although this is a "__" function, it was historically in the
  * public ABI, so we can never change it or remove its signature, even
  * though it is now only used in AMP_DEBUG mode. */
void
amp_err__locate(const char* file, long line, const char* function)
{
#ifdef AMP_DEBUG
	last_error_file = file;
	last_error_line = line;
	last_error_function = function;
#endif
}


/* Cleanup function for errors.  amp_err_clear () removes this so
   errors that are properly handled *don't* hit this code. */
#ifdef AMP_DEBUG
static int err_abort(void* data)
{
	amp_err_t* err = (amp_err_t*)data;  /* For easy viewing in a debugger */
	AMP_UNUSED(err);

#ifdef WIN32
	__debugbreak();
#else
	abort();
#endif
	return AMP_SUCCESS;
}
#endif


static amp_err_t*
make_error_internal(amp_status_t status,
					amp_err_t* child)
{
	amp_pool_t* pool = nullptr;
	amp_err_t* new_error;

	/* Reuse the child's pool, or create our own. */
	if (child)
		pool = child->pool;

	if (!pool)
	{
		pool = amp_pool_create(NULL);
		if (!pool)
			abort();
	}

	/* Create the new error structure */
	new_error = amp_pcalloc<amp_err_t>(pool);

	/* Fill 'er up. */
	new_error->status = status;
	new_error->child = child;
	new_error->pool = pool;

#ifdef AMP_DEBUG
	// Tracing support
	new_error->file = last_error_file;
	new_error->line = last_error_line;
	new_error->func = last_error_function;
#endif // AMP_DEBUG


#ifdef AMP_DEBUG
	// Error leak detection
	if (!child || !child->pool)
		amp_pool_cleanup_register(pool, new_error, err_abort, nullptr);
#endif /* AMP_DEBUG */

	return new_error;
}



/*** Creating and destroying errors. ***/

amp_err_t*
amp_err_create(amp_status_t amp_err,
				 amp_err_t* child,
				 const char* message)
{
	amp_err_t* err;

	err = make_error_internal(amp_err, child);

	if (message)
		err->message = amp_pstrdup(message, err->pool);

	return err;
}


amp_err_t*
amp_err_createf(amp_status_t amp_err,
				  amp_err_t* child,
				  const char* fmt,
				  ...)
{
	amp_err_t* err;
	va_list ap;

	err = make_error_internal(amp_err, child);

	va_start(ap, fmt);
	err->message = amp_pvsprintf(err->pool, fmt, ap);
	va_end(ap);

	return err;
}


amp_err_t*
amp_err_wrap_status(
		amp_status_t status,
				   const char* fmt,
				   ...)
{
	amp_err_t* err;
	va_list ap;

	err = make_error_internal(status, nullptr);

	if (fmt)
	{
		const char* status_msg = amp_err_str_from_status(status, err->pool);
		const char* msg;

		/* Append it to the formatted message. */
		va_start(ap, fmt);
		msg = amp_pvsprintf(err->pool, fmt, ap);
		va_end(ap);
		if (status_msg && *status_msg)
		{
			err->message = amp_pstrcat(err->pool, msg, ": ", status_msg,
									   AMP_VA_NULL);
		}
		else
		{
			err->message = msg;
		}
	}

	return err;
}


amp_err_t*
amp_err_quick_wrap(amp_err_t* child, const char* new_msg)
{
	if (child == AMP_NO_ERROR)
		return AMP_NO_ERROR;

	return amp_err_create(child->status,
							child,
							new_msg);
}

amp_err_t*
amp_err_quick_wrapf(amp_err_t* child,
					  const char* fmt,
					  ...)
{
	amp_err_t* err;
	va_list ap;

	if (child == AMP_NO_ERROR)
		return AMP_NO_ERROR;

	err = make_error_internal(child->status, child);

	va_start(ap, fmt);
	err->message = amp_pvsprintf(err->pool, fmt, ap);
	va_end(ap);

	return err;
}

/* Messages in tracing errors all point to this static string. */
static const char error_tracing_link[] = "traced call";

amp_err_t*
amp_err__trace(const char* file, long line, const char* function, amp_err_t* err)
{
#ifndef AMP_DEBUG

	/* We shouldn't even be here, but whatever. Just return the error as-is.  */
	return err;

#else

	/* Only do the work when an error occurs.  */
	if (err)
	{
		amp_err_t* trace;
		amp_err__locate(file, line, function);
		trace = make_error_internal(err->status, err);
		trace->message = error_tracing_link;
		trace->is_trace = true;

		return trace;
	}
	return AMP_NO_ERROR;

#endif
}


static void
amp_err_compose(amp_err_t* chain, amp_err_t* new_err)
{
	amp_pool_t* pool = chain->pool;
	amp_pool_t* oldpool = new_err->pool;

	while (chain->child)
		chain = chain->child;

#if defined(AMP_DEBUG)
	/* Kill existing handler since the end of the chain is going to change */
	amp_pool_cleanup_kill(pool, chain, err_abort);
#endif

	/* Copy the new error chain into the old chain's pool. */
	while (new_err)
	{
		chain->child = amp_palloc<amp_err_t>(pool);
		chain = chain->child;
		*chain = *new_err;
		if (chain->message)
			chain->message = amp_pstrdup(new_err->message, pool);
		if (chain->file)
			chain->file = amp_pstrdup(new_err->file, pool);
		chain->pool = pool;
		chain->is_composed = true;
#if defined(AMP_DEBUG)
		if (!new_err->child)
			amp_pool_cleanup_kill(oldpool, new_err, err_abort);
#endif
		new_err = new_err->child;
	}

#if defined(AMP_DEBUG)
	amp_pool_cleanup_register(pool, chain, err_abort, nullptr);
#endif

	/* Destroy the new error chain. */
	amp_pool_destroy(oldpool);
}

amp_err_t*
amp_err_compose_create(amp_err_t* err1,
						 amp_err_t* err2)
{
	if (err1 && err2)
	{
		amp_err_compose(err1,
						  amp_err_create(AMP_ERR_COMPOSED_ERROR, err2, NULL));
		return err1;
	}
	return err1 ? err1 : err2;
}


amp_err_t*
amp_err_root_cause(amp_err_t* err)
{
	while (err)
	{
		/* I don't think we can change the behavior here, but the additional
		   error chain doesn't define the root cause. Perhaps we should rev
		   this function. */
		if (err->child && !err->child->is_composed)
			err = err->child;
		else
			break;
	}

	return err;
}

amp_err_t*
amp_err_find_cause(amp_err_t* err, amp_status_t status)
{
	amp_err_t* child;

	for (child = err; child; child = child->child)
		if (child->status == status)
			return child;

	return AMP_NO_ERROR;
}

amp_err_t*
amp_err_dup(const amp_err_t* err)
{
	amp_pool_t* pool;
	amp_err_t* new_err = nullptr, * tmp_err = nullptr;

	if (!err)
		return AMP_NO_ERROR;

	pool = amp_pool_create(nullptr);
	if (!pool)
		abort();

	for (; err; err = err->child)
	{
		if (!new_err)
		{
			new_err = amp_palloc<amp_err_t>(pool);
			tmp_err = new_err;
		}
		else
		{
			tmp_err->child = amp_palloc<amp_err_t>(pool);
			tmp_err = tmp_err->child;
		}
		*tmp_err = *err;
		tmp_err->pool = pool;
		if (tmp_err->message)
			tmp_err->message = amp_pstrdup(tmp_err->message, pool);
		if (tmp_err->file)
			tmp_err->file = amp_pstrdup(tmp_err->file, pool);
	}

#if defined(AMP_DEBUG)
	amp_pool_cleanup_register(pool, tmp_err, err_abort, nullptr);
#endif

	return new_err;
}

void
amp_err_clear(amp_err_t* err)
{
	if (err)
	{
#if defined(AMP_DEBUG)
		while (err->child)
			err = err->child;
		amp_pool_cleanup_kill(err->pool, err, err_abort);
#endif
		amp_pool_destroy(err->pool);
	}
}

amp_err_t*
amp_err_purge_tracing(amp_err_t* err)
{
#ifdef AMP_ERR__TRACING
	amp_err_t* new_err = NULL, * new_err_leaf = NULL;

	if (!err)
		return AMP_NO_ERROR;

	do
	{
		amp_err_t* tmp_err;

		/* Skip over any trace-only links. */
		while (err && err->is_trace)
			err = err->child;

		/* The link must be a real link in the error chain, otherwise an
		   error chain with trace only links would map into AMP_NO_ERROR. */
		if (!err)
			return amp_err_create(AMP_ERR_MALFUNCTION, nullptr, nullptr);

		/* Copy the current error except for its child error pointer
		   into the new error.  Share any message and source filename
		   strings from the error. */
		tmp_err = amp_pcalloc<amp_err_t>(err->pool);
		*tmp_err = *err;
		tmp_err->child = NULL;

		/* Add a new link to the new chain (creating the chain if necessary). */
		if (!new_err)
		{
			new_err = tmp_err;
			new_err_leaf = tmp_err;
		}
		else
		{
			new_err_leaf->child = tmp_err;
			new_err_leaf = tmp_err;
		}

		/* Advance to the next link in the original chain. */
		err = err->child;
	} while (err);

	return new_err;
#else  /* AMP_ERR__TRACING */
	return err;
#endif /* AMP_ERR__TRACING */
}


AMP_DECLARE(amp_err_t*)
amp_cmdline_printf(amp_pool_t* scratch_pool, const char* fmt, ...);

AMP_DECLARE(amp_err_t*)
amp_cmdline_puts(const char* text, amp_pool_t* scratch_pool);

/* ### The logic around omitting (sic) amp_err= in maintainer mode is tightly
   ### coupled to the current sole caller.*/
static void
print_error(amp_err_t* err, FILE* stream, const char* prefix)
{
	const char* err_string;
	amp_err_t* temp_err = NULL;  /* ensure initialized even if
									  err->file == NULL */
									  /* Pretty-print the error */
									  /* Note: we can also log errors here someday. */

#ifdef _DEBUG
  /* Note: err->file is _not_ in UTF-8, because it's expanded from
		   the __FILE__ preprocessor macro. */
	const char* file_utf8;

	if (err->file
		&& !(temp_err = amp_cstring_to_utf8(&file_utf8, err->file,
											err->pool)))

		amp_err_clear(amp_cmdline_printf(err->pool,
										 "%s:%ld:%s", err->file, err->line, err->func));
	else
	{
		amp_err_clear(amp_cmdline_puts("",
									   err->pool));
		amp_err_clear(temp_err);
	}

	{
		const char* symbolic_name;
		if (err->is_trace)
			/* Skip it; the error code will be printed by the real link. */
			amp_err_clear(amp_cmdline_printf(err->pool, ",\n"));
		else if ((symbolic_name = amp_err_symbolic_name(err->status)))
			amp_err_clear(amp_cmdline_printf(err->pool,
											 ": (amp_err=%s)\n", symbolic_name));
		else
			amp_err_clear(amp_cmdline_printf(err->pool,
											 ": (amp_err=%d)\n", err->status));
	}
#endif /* AMP_DEBUG */

	/* "traced call" */
	if (err->is_trace)
	{
		/* Skip it.  We already printed the file-line coordinates. */
	}
	/* Only print the same APR error string once. */
	else if (err->message)
	{
		amp_err_clear(amp_cmdline_printf(err->pool,
										 "%sE%06d: %s\n",
										 prefix, err->status, err->message));
	}
	else
	{
		/* Is this a Subversion-specific error code? */
		if ((err->status > AMP_OS_START_USEERR)
			&& (err->status <= AMP_OS_START_CANONERR))
			err_string = amp_err_str_from_status(err->status, err->pool);
		/* Otherwise, this must be an APR error code. */
		else if ((temp_err = amp_cstring_to_utf8
		(&err_string, "Unknown error value", err->pool)))
		{
			amp_err_clear(temp_err);
			err_string = _("Can't recode error string from APR");
		}

		amp_err_clear(amp_cmdline_printf(err->pool,
										 "%sE%06d: %s\n",
										 prefix, err->status, err_string));
	}
}


const char*
amp_err_best_message(amp_err_t* err, char* buf, size_t bufsize)
{
	if (!err)
		return "";

	/* Skip over any trace records.  */
	while (err->is_trace)
		err = err->child;

	if (!err->message && err->message_func)
		err->message = (*err->message_func)(err, err->pool);

	if (err->message)
		return err->message;
	else
		return amp_err_str_from_status(err->status, err->pool);
}

const char*
amp_err_message(amp_err_t* err)
{
	if (!err)
		return "";
	if (err->message)
		return err->message;
	else if (err->message_func)
		return err->message = (*err->message_func)(err, err->pool);

	return amp_err_str_from_status(err->status, err->pool);
}


/* amp_strerror() and helpers */

/* Duplicate of the same typedef in tests/libamp_subr/error-code-test.c */
typedef struct err_defn {
	amp_status_t errcode; /* 160004 */
	const char* errname; /* AMP_ERR_FS_CORRUPT */
	const char* errdesc; /* default message */
} err_defn;

/* To understand what is going on here, read amp_err_codes.h. */
#define AMP_ERROR_BUILD_ARRAY
#include "amp_error_codes.h"

const char*
amp_err_str_from_status(amp_status_t status_code, amp_pool_t* pool)
{
	const err_defn* defn;

	// TODO: Implement binary search or something (and validate ordering in tests)
	for (defn = error_table; defn->errdesc != NULL; ++defn)
		if (defn->errcode == (amp_errno_t)status_code)
		{
			return _(defn->errdesc);
		}

#ifdef _WIN32
	const int sz = 256;
	wchar_t staticBuffer[sz + 1];

	DWORD len = FormatMessageW(
		FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
		NULL /* source */,
		AMP_ERR_TO_OS(status_code),
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), /* Default language */
		staticBuffer, sz,
		nullptr /* va_args */);

	if (len > 0)
	{
		staticBuffer[len < sz ? len : sz - 1] = 0;
		while (len > 1 && (staticBuffer[len - 1] == '\n' || staticBuffer[len - 1] == '\r'))
			staticBuffer[--len] = 0;

		return amp_wchar_to_utf8(staticBuffer, pool);
	}
#endif

	return amp_psprintf(pool, "Unknown error %I64d", status_code);
}

#ifdef AMP_HAVE_ERRORCODES
/* Defines amp__errno and amp__apr_errno */
#include "errorcode.inc"
#endif

const char*
amp_err_symbolic_name(amp_status_t statcode)
{
	const err_defn* defn;
#ifdef AMP_HAVE_ERRORCODES
	int i;
#endif /* AMP_DEBUG */

	for (defn = error_table; defn->errdesc != NULL; ++defn)
		if (defn->errcode == (amp_errno_t)statcode)
			return defn->errname;

	/* "No error" is not in error_table. */
	if (statcode == AMP_SUCCESS)
		return "AMP_SUCCESS";

#ifdef AMP_HAVE_ERRORCODES
	/* Try errno.h symbols. */
	/* Linear search through a sorted array */
	for (i = 0; i < sizeof(amp__errno) / sizeof(amp__errno[0]); i++)
		if (amp__errno[i].errcode == (int)statcode)
			return amp__errno[i].errname;

	/* Try APR errors. */
	/* Linear search through a sorted array */
	for (i = 0; i < sizeof(amp__apr_errno) / sizeof(amp__apr_errno[0]); i++)
		if (amp__apr_errno[i].errcode == (int)statcode)
			return amp__apr_errno[i].errname;
#endif /* AMP_DEBUG */

	/* ### TODO: do we need AMP_* error macros?  What about AMP_TO_OS_ERROR()? */

	return NULL;
}



/* Malfunctions. */

amp_err_t*
amp_err_raise_on_malfunction(amp_boolean_t can_return,
							   const char* file, int line, const char* function,
							   const char* expr)
{
	if (!can_return)
		abort(); /* Nothing else we can do as a library */

	  /* The filename and line number of the error source needs to be set
		 here because amp_err_createf() is not the macro defined in
		 amp_error.h but the real function. */
	amp_err__locate(file, line, function);

	if (expr)
		return amp_err_createf(AMP_ERR_ASSERTION_FAIL, NULL,
								 _("In file '%s' line %d: assertion failed (%s)"),
								 file, line, expr);
	else
		return amp_err_createf(AMP_ERR_ASSERTION_FAIL, NULL,
								 _("In file '%s' line %d: internal malfunction"),
								 file, line);
}

amp_err_t*
amp_err_abort_on_malfunction(amp_boolean_t can_return,
							   const char* file, int line, const char* function,
							   const char* expr)
{
	amp_err_t* err = amp_err_raise_on_malfunction(true, file, line, function, expr);

	//amp_handle_error2(err, stderr, FALSE, "svn: ");
	abort();
	return err;  /* Not reached. */
}



