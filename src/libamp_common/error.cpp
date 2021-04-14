#include <stdarg.h>
#include "common.hpp"
#include "amp_pools.hpp"

const char*
amp_error_symbolic_name(amp_status_t statcode);


/*
 * Undefine the helpers for creating errors.
 *
 * *NOTE*: Any use of these functions in any other function may need
 * to call amp_error__locate() because the macro that would otherwise
 * do this is being undefined and the filename and line number will
 * not be properly set in the static error_file and error_line
 * variables.
 */
#undef amp_error_create
#undef amp_error_createf
#undef amp_error_quick_wrap
#undef amp_error_quick_wrapf
#undef amp_error_wrap_apr

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
 * to call amp_error__locate() because the macro that would otherwise
 * do this is being undefined and the filename and line number will
 * not be properly set in the static error_file and error_line
 * variables.
 */
#undef amp_error_create
#undef amp_error_createf
#undef amp_error_quick_wrap
#undef amp_error_quick_wrapf
#undef amp_error_wrap_apr

 /* Note: Although this is a "__" function, it was historically in the
  * public ABI, so we can never change it or remove its signature, even
  * though it is now only used in AMP_DEBUG mode. */
void
amp_error__locate(const char* file, long line, const char* function)
{
#ifdef AMP_DEBUG
	last_error_file = file;
	last_error_line = line;
	last_error_function = function;
#endif
}


/* Cleanup function for errors.  amp_error_clear () removes this so
   errors that are properly handled *don't* hit this code. */
#ifdef AMP_DEBUG
static int err_abort(void* data)
{
	amp_error_t* err = (amp_error_t*)data;  /* For easy viewing in a debugger */
	AMP_UNUSED(err);

	if (!getenv("amp_DBG_NO_ABORT_ON_ERROR_LEAK"))
		abort();
	return APR_SUCCESS;
}
#endif


static amp_error_t*
make_error_internal(amp_status_t status,
					amp_error_t* child)
{
	amp_pool_t* pool;
	amp_error_t* new_error;

	/* Reuse the child's pool, or create our own. */
	if (child)
		pool = child->pool;
	else
	{
		pool = amp_pool_create(NULL);
		if (!pool)
			abort();
	}

	/* Create the new error structure */
	new_error = amp_pcalloc<amp_error_t>(pool);

	/* Fill 'er up. */
	new_error->status = status;
	new_error->child = child;
	new_error->pool = pool;

#ifdef AMP_DEBUG
	// Tracing support
	new_error->file = last_error_file;
	new_error->line = last_error_line;
#endif // AMP_DEBUG


#ifdef AMP_DEBUG
	// Error leak detection
	if (!child)
		amp_pool_cleanup_register(pool, new_error, err_abort, nullptr);
#endif /* AMP_DEBUG */

	return new_error;
}



/*** Creating and destroying errors. ***/

amp_error_t*
amp_error_create(amp_status_t apr_err,
				 amp_error_t* child,
				 const char* message)
{
	amp_error_t* err;

	err = make_error_internal(apr_err, child);

	if (message)
		err->message = amp_pstrdup(message, err->pool);

	return err;
}


amp_error_t*
amp_error_createf(amp_status_t apr_err,
				  amp_error_t* child,
				  const char* fmt,
				  ...)
{
	amp_error_t* err;
	va_list ap;

	err = make_error_internal(apr_err, child);

	va_start(ap, fmt);
	err->message = amp_pvprintf(err->pool, fmt, ap);
	va_end(ap);

	return err;
}


amp_error_t*
amp_error_wrap_apr(amp_status_t status,
				   const char* fmt,
				   ...)
{
	amp_error_t* err;
	va_list ap;
	char errbuf[255];
	const char* msg_apr, * msg;

	err = make_error_internal(status, NULL);

	if (fmt)
	{
		/* Grab the APR error message. */
		snprintf(errbuf, sizeof(errbuf), "Raw error %I64d", status);
		//strncpy(status, errbuf, sizeof(errbuf));
		//utf8_err = errbuf;//amp_cstring_to_utf8(&msg_apr, errbuf, err->pool);
		//if (utf8_err)
		//    msg_apr = NULL;
		msg_apr = errbuf;
		//amp_error_clear(utf8_err);

		/* Append it to the formatted message. */
		va_start(ap, fmt);
		msg = amp_pvprintf(err->pool, fmt, ap);
		va_end(ap);
		if (msg_apr)
		{
			err->message = amp_pstrcat(err->pool, msg, ": ", msg_apr,
									   AMP_VA_NULL);
		}
		else
		{
			err->message = msg;
		}
	}

	return err;
}


amp_error_t*
amp_error_quick_wrap(amp_error_t* child, const char* new_msg)
{
	if (child == AMP_NO_ERROR)
		return AMP_NO_ERROR;

	return amp_error_create(child->status,
							child,
							new_msg);
}

amp_error_t*
amp_error_quick_wrapf(amp_error_t* child,
					  const char* fmt,
					  ...)
{
	amp_error_t* err;
	va_list ap;

	if (child == AMP_NO_ERROR)
		return AMP_NO_ERROR;

	err = make_error_internal(child->status, child);

	va_start(ap, fmt);
	err->message = amp_pvprintf(err->pool, fmt, ap);
	va_end(ap);

	return err;
}

/* Messages in tracing errors all point to this static string. */
static const char error_tracing_link[] = "traced call";

amp_error_t*
amp_error__trace(const char* file, long line, const char* function, amp_error_t* err)
{
#ifndef AMP_DEBUG

	/* We shouldn't even be here, but whatever. Just return the error as-is.  */
	return err;

#else

	/* Only do the work when an error occurs.  */
	if (err)
	{
		amp_error_t* trace;
		amp_error__locate(file, line, function);
		trace = make_error_internal(err->status, err);
		trace->message = error_tracing_link;
		trace->is_trace = true;

		return trace;
	}
	return AMP_NO_ERROR;

#endif
}


static void
amp_error_compose(amp_error_t* chain, amp_error_t* new_err)
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
		chain->child = amp_palloc<amp_error_t>(pool);
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

amp_error_t*
amp_error_compose_create(amp_error_t* err1,
						 amp_error_t* err2)
{
	if (err1 && err2)
	{
		amp_error_compose(err1,
						  amp_error_create(AMP_ERR_COMPOSED_ERROR, err2, NULL));
		return err1;
	}
	return err1 ? err1 : err2;
}


amp_error_t*
amp_error_root_cause(amp_error_t* err)
{
	while (err)
	{
		/* I don't think we can change the behavior here, but the additional
		   error chain doesn't define the root cause. Perhaps we should rev
		   this function. */
		if (err->child /*&& err->child->apr_err != amp_ERR_COMPOSED_ERROR*/)
			err = err->child;
		else
			break;
	}

	return err;
}

amp_error_t*
amp_error_find_cause(amp_error_t* err, amp_status_t status)
{
	amp_error_t* child;

	for (child = err; child; child = child->child)
		if (child->status == status)
			return child;

	return AMP_NO_ERROR;
}

amp_error_t*
amp_error_dup(const amp_error_t* err)
{
	amp_pool_t* pool;
	amp_error_t* new_err = nullptr, * tmp_err = nullptr;

	if (!err)
		return AMP_NO_ERROR;

	pool = amp_pool_create(nullptr);
	if (!pool)
		abort();

	for (; err; err = err->child)
	{
		if (!new_err)
		{
			new_err = amp_palloc<amp_error_t>(pool);
			tmp_err = new_err;
		}
		else
		{
			tmp_err->child = amp_palloc<amp_error_t>(pool);
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
amp_error_clear(amp_error_t* err)
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

amp_boolean_t
amp_error__is_tracing_link(const amp_error_t* err)
{
#ifdef amp_ERR__TRACING
	/* ### A strcmp()?  Really?  I think it's the best we can do unless
	   ### we add a boolean field to amp_error_t that's set only for
	   ### these "placeholder error chain" items.  Not such a bad idea,
	   ### really...  */
	return (err && err->message && !strcmp(err->message, error_tracing_link));
#else
	return FALSE;
#endif
}

amp_error_t*
amp_error_purge_tracing(amp_error_t* err)
{
#ifdef amp_ERR__TRACING
	amp_error_t* new_err = NULL, * new_err_leaf = NULL;

	if (!err)
		return AMP_NO_ERROR;

	do
	{
		amp_error_t* tmp_err;

		/* Skip over any trace-only links. */
		while (err && amp_error__is_tracing_link(err))
			err = err->child;

		/* The link must be a real link in the error chain, otherwise an
		   error chain with trace only links would map into AMP_NO_ERROR. */
		if (!err)
			return amp_error_create(AMP_ERR_MALFUNCTION, nullptr, nullptr);

		/* Copy the current error except for its child error pointer
		   into the new error.  Share any message and source filename
		   strings from the error. */
		tmp_err = (amp_error_t*)apr_palloc(err->pool, sizeof(*tmp_err));
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
#else  /* amp_ERR__TRACING */
	return err;
#endif /* amp_ERR__TRACING */
}


AMP_DECLARE(amp_error_t*)
amp_cmdline_printf(amp_pool_t* scratch_pool, const char* fmt, ...);

AMP_DECLARE(amp_error_t*)
amp_cmdline_puts(const char* text, amp_pool_t *scratch_pool);

/* ### The logic around omitting (sic) apr_err= in maintainer mode is tightly
   ### coupled to the current sole caller.*/
static void
print_error(amp_error_t* err, FILE* stream, const char* prefix)
{
	char errbuf[256];
	const char* err_string;
	amp_error_t* temp_err = NULL;  /* ensure initialized even if
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

		amp_error_clear(amp_cmdline_printf(err->pool,
										   "%s:%ld", err->file, err->line));
	else
	{
		amp_error_clear(amp_cmdline_puts("",
										 err->pool));
		amp_error_clear(temp_err);
	}

	{
		const char* symbolic_name;
		if (amp_error__is_tracing_link(err))
			/* Skip it; the error code will be printed by the real link. */
			amp_error_clear(amp_cmdline_printf(err->pool, ",\n"));
		else if ((symbolic_name = amp_error_symbolic_name(err->status)))
			amp_error_clear(amp_cmdline_printf(err->pool,
											   ": (apr_err=%s)\n", symbolic_name));
		else
			amp_error_clear(amp_cmdline_printf(err->pool,
											   ": (apr_err=%d)\n", err->status));
	}
#endif /* AMP_DEBUG */

	/* "traced call" */
	if (amp_error__is_tracing_link(err))
	{
		/* Skip it.  We already printed the file-line coordinates. */
	}
	/* Only print the same APR error string once. */
	else if (err->message)
	{
		amp_error_clear(amp_cmdline_printf(err->pool,
										   "%sE%06d: %s\n",
										   prefix, err->status, err->message));
	}
	else
	{
		/* Is this a Subversion-specific error code? */
		if ((err->status > APR_OS_START_USEERR)
			&& (err->status <= APR_OS_START_CANONERR))
			err_string = amp_strerror(err->status, errbuf, sizeof(errbuf));
		/* Otherwise, this must be an APR error code. */
		else if ((temp_err = amp_cstring_to_utf8
		(&err_string, "Unknown error value", err->pool)))
		{
			amp_error_clear(temp_err);
			err_string = _("Can't recode error string from APR");
		}

		amp_error_clear(amp_cmdline_printf(err->pool,
										   "%sE%06d: %s\n",
										   prefix, err->status, err_string));
	}
}


const char*
amp_err_best_message(amp_error_t* err, char* buf, apr_size_t bufsize)
{
	/* Skip over any trace records.  */
	while (amp_error__is_tracing_link(err))
		err = err->child;

	if (!err->message && err->message_func)
		err->message = (*err->message_func)(err, err->pool);

	if (err->message)
		return err->message;
	else
		return amp_strerror(err->status, buf, bufsize);
}


/* amp_strerror() and helpers */

/* Duplicate of the same typedef in tests/libamp_subr/error-code-test.c */
typedef struct err_defn {
	amp_errno_t errcode; /* 160004 */
	const char* errname; /* amp_ERR_FS_CORRUPT */
	const char* errdesc; /* default message */
} err_defn;

/* To understand what is going on here, read amp_error_codes.h. */
#define AMP_ERROR_BUILD_ARRAY
#include "amp_error_codes.h"

char*
amp_strerror(amp_status_t statcode, char* buf, apr_size_t bufsize)
{
	const err_defn* defn;

	for (defn = error_table; defn->errdesc != NULL; ++defn)
		if (defn->errcode == (amp_errno_t)statcode)
		{
			strncpy(buf, _(defn->errdesc), bufsize - 1);
			buf[bufsize - 1] = '\0';
			return buf;
		}

	return strncpy(buf, "APR error", bufsize);//  apr_strerror(statcode, buf, bufsize);
}

#ifdef AMP_HAVE_ERRORCODES
/* Defines amp__errno and amp__apr_errno */
#include "errorcode.inc"
#endif

const char*
amp_error_symbolic_name(apr_status_t statcode)
{
	const err_defn* defn;
#ifdef AMP_HAVE_ERRORCODES
	int i;
#endif /* AMP_DEBUG */

	for (defn = error_table; defn->errdesc != NULL; ++defn)
		if (defn->errcode == (amp_errno_t)statcode)
			return defn->errname;

	/* "No error" is not in error_table. */
	if (statcode == APR_SUCCESS)
		return "AMP_NO_ERROR";

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

	/* ### TODO: do we need APR_* error macros?  What about APR_TO_OS_ERROR()? */

	return NULL;
}



/* Malfunctions. */

amp_error_t*
amp_error_raise_on_malfunction(amp_boolean_t can_return,
							   const char* file, int line, const char *function,
							   const char* expr)
{
	if (!can_return)
		abort(); /* Nothing else we can do as a library */

	  /* The filename and line number of the error source needs to be set
		 here because amp_error_createf() is not the macro defined in
		 amp_error.h but the real function. */
	amp_error__locate(file, line, function);

	if (expr)
		return amp_error_createf(AMP_ERR_ASSERTION_FAIL, NULL,
								 _("In file '%s' line %d: assertion failed (%s)"),
								 file, line, expr);
	else
		return amp_error_createf(AMP_ERR_ASSERTION_FAIL, NULL,
								 _("In file '%s' line %d: internal malfunction"),
								 file, line);
}

amp_error_t*
amp_error_abort_on_malfunction(amp_boolean_t can_return,
							   const char* file, int line, const char *function,
							   const char* expr)
{
	amp_error_t* err = amp_error_raise_on_malfunction(TRUE, file, line, function, expr);

	//amp_handle_error2(err, stderr, FALSE, "svn: ");
	abort();
	return err;  /* Not reached. */
}



