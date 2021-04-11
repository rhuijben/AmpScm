#include <stdarg.h>
#include "common.hpp"
#include <apr_general.h>
#include <apr_pools.h>
#include <apr_strings.h>
#include "amp_pools.hpp"

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

#if defined(_DEBUG) && APR_HAS_THREADS
#include <apr_thread_proc.h>
#endif

#include <zlib.h>

#ifndef amp_ERR__TRACING
#define amp_ERR__TRACING
#endif

#ifdef _DEBUG
#  if APR_HAS_THREADS
static apr_threadkey_t* error_file_key = NULL;
static apr_threadkey_t* error_line_key = NULL;

/* No-op destructor for apr_threadkey_private_create(). */
static void null_threadkey_dtor(void* stuff) {}

/* Implements amp_atomic__str_init_func_t.
   Callback used by amp_error__locate to initialize the thread-local
   error location storage.  This function will never return an
   error string. */
static const char*
locate_init_once(void* ignored_baton)
{
    /* Strictly speaking, this is a memory leak, since we're creating an
       unmanaged, top-level pool and never destroying it.  We do this
       because this pool controls the lifetime of the thread-local
       storage for error locations, and that storage must always be
       available. */
    apr_pool_t* threadkey_pool = amp_pool_create_unmanaged(TRUE);
    apr_status_t status;

    status = apr_threadkey_private_create(&error_file_key,
                                          null_threadkey_dtor,
                                          threadkey_pool);
    if (status == APR_SUCCESS)
        status = apr_threadkey_private_create(&error_line_key,
                                              null_threadkey_dtor,
                                              threadkey_pool);

    /* If anything went wrong with the creation of the thread-local
       storage, we'll revert to the old, thread-agnostic behaviour */
    if (status != APR_SUCCESS)
        error_file_key = error_line_key = NULL;

    return NULL;
}
#  endif  /* APR_HAS_THREADS */

/* These location variables will be used in no-threads mode or if
   thread-local storage is not available. */
static const char* volatile error_file = NULL;
static long volatile error_line = -1;

/* file_line for the non-debug case. */
static const char AMP_FILE_LINE_UNDEFINED[] = "amp:<undefined>";
#endif /* amp_DEBUG */


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
  * though it is now only used in amp_DEBUG mode. */
void
amp_error__locate(const char* file, long line)
{
#ifdef amp_DEBUG
#  if APR_HAS_THREADS
    static volatile amp_atomic_t init_status = 0;
    amp_atomic__init_once_no_error(&init_status, locate_init_once, NULL);

    if (error_file_key && error_line_key)
    {
        apr_status_t status;
        status = apr_threadkey_private_set((char*)file, error_file_key);
        if (status == APR_SUCCESS)
            status = apr_threadkey_private_set((void*)line, error_line_key);
        if (status == APR_SUCCESS)
            return;
    }
#  endif  /* APR_HAS_THREADS */

    error_file = file;
    error_line = line;
#endif /* amp_DEBUG */
}


/* Cleanup function for errors.  amp_error_clear () removes this so
   errors that are properly handled *don't* hit this code. */
#ifdef amp_DEBUG
static apr_status_t err_abort(void* data)
{
    amp_error_t* err = data;  /* For easy viewing in a debugger */
    amp_UNUSED(err);

    if (!getenv("amp_DBG_NO_ABORT_ON_ERROR_LEAK"))
        abort();
    return APR_SUCCESS;
}
#endif


static amp_error_t*
make_error_internal(apr_status_t apr_err,
                    amp_error_t* child)
{
    apr_pool_t* pool;
    amp_error_t* new_error;
#ifdef amp_DEBUG
    apr_status_t status = APR_ENOTIMPL;
#endif

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
    new_error = (amp_error_t*)apr_pcalloc(pool, sizeof(*new_error));

    /* Fill 'er up. */
    new_error->apr_err = apr_err;
    new_error->child = child;
    new_error->pool = pool;

#ifdef amp_DEBUG
#if APR_HAS_THREADS
    if (error_file_key && error_line_key)
    {
        void* item;
        status = apr_threadkey_private_get(&item, error_file_key);
        if (status == APR_SUCCESS)
        {
            new_error->file = item;
            status = apr_threadkey_private_get(&item, error_line_key);
            if (status == APR_SUCCESS)
                new_error->line = (long)item;
        }
    }
#  endif  /* APR_HAS_THREADS */

    if (status != APR_SUCCESS)
    {
        new_error->file = error_file;
        new_error->line = error_line;
    }

    if (!child)
        apr_pool_cleanup_register(pool, new_error,
                                  err_abort,
                                  apr_pool_cleanup_null);
#endif /* amp_DEBUG */

    return new_error;
}



/*** Creating and destroying errors. ***/

amp_error_t*
amp_error_create(apr_status_t apr_err,
                 amp_error_t* child,
                 const char* message)
{
    amp_error_t* err;

    err = make_error_internal(apr_err, child);

    if (message)
        err->message = apr_pstrdup(err->pool, message);

    return err;
}


amp_error_t*
amp_error_createf(apr_status_t apr_err,
                  amp_error_t* child,
                  const char* fmt,
                  ...)
{
    amp_error_t* err;
    va_list ap;

    err = make_error_internal(apr_err, child);

    va_start(ap, fmt);
    err->message = apr_pvsprintf(err->pool, fmt, ap);
    va_end(ap);

    return err;
}


amp_error_t*
amp_error_wrap_apr(apr_status_t status,
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
        apr_strerror(status, errbuf, sizeof(errbuf));
        //utf8_err = errbuf;//amp_cstring_to_utf8(&msg_apr, errbuf, err->pool);
        //if (utf8_err)
        //    msg_apr = NULL;
        msg_apr = errbuf;
        //amp_error_clear(utf8_err);

        /* Append it to the formatted message. */
        va_start(ap, fmt);
        msg = apr_pvsprintf(err->pool, fmt, ap);
        va_end(ap);
        if (msg_apr)
        {
            err->message = apr_pstrcat(err->pool, msg, ": ", msg_apr,
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

    return amp_error_create(child->apr_err,
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

    err = make_error_internal(child->apr_err, child);

    va_start(ap, fmt);
    err->message = apr_pvsprintf(err->pool, fmt, ap);
    va_end(ap);

    return err;
}

/* Messages in tracing errors all point to this static string. */
static const char error_tracing_link[] = "traced call";

amp_error_t*
amp_error__trace(const char* file, long line, amp_error_t* err)
{
#ifndef amp_DEBUG

    /* We shouldn't even be here, but whatever. Just return the error as-is.  */
    return err;

#else

    /* Only do the work when an error occurs.  */
    if (err)
    {
        amp_error_t* trace;
        amp_error__locate(file, line);
        trace = make_error_internal(err->apr_err, err);
        trace->message = error_tracing_link;
        return trace;
    }
    return AMP_NO_ERROR;

#endif
}


static void
amp_error_compose(amp_error_t* chain, amp_error_t* new_err)
{
    apr_pool_t* pool = chain->pool;
    apr_pool_t* oldpool = new_err->pool;

    while (chain->child)
        chain = chain->child;

#if defined(amp_DEBUG)
    /* Kill existing handler since the end of the chain is going to change */
    apr_pool_cleanup_kill(pool, chain, err_abort);
#endif

    /* Copy the new error chain into the old chain's pool. */
    while (new_err)
    {
        chain->child = (amp_error_t*)apr_palloc(pool, sizeof(*chain->child));
        chain = chain->child;
        *chain = *new_err;
        if (chain->message)
            chain->message = apr_pstrdup(pool, new_err->message);
        if (chain->file)
            chain->file = apr_pstrdup(pool, new_err->file);
        chain->pool = pool;
#if defined(amp_DEBUG)
        if (!new_err->child)
            apr_pool_cleanup_kill(oldpool, new_err, err_abort);
#endif
        new_err = new_err->child;
    }

#if defined(amp_DEBUG)
    apr_pool_cleanup_register(pool, chain,
                              err_abort,
                              apr_pool_cleanup_null);
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
amp_error_find_cause(amp_error_t* err, apr_status_t apr_err)
{
    amp_error_t* child;

    for (child = err; child; child = child->child)
        if (child->apr_err == apr_err)
            return child;

    return AMP_NO_ERROR;
}

amp_error_t*
amp_error_dup(const amp_error_t* err)
{
    apr_pool_t* pool;
    amp_error_t* new_err = NULL, * tmp_err = NULL;

    if (!err)
        return AMP_NO_ERROR;

    pool = amp_pool_create(NULL);
    if (!pool)
        abort();

    for (; err; err = err->child)
    {
        if (!new_err)
        {
            new_err = (amp_error_t*)apr_palloc(pool, sizeof(*new_err));
            tmp_err = new_err;
        }
        else
        {
            tmp_err->child = (amp_error_t*)apr_palloc(pool, sizeof(*tmp_err->child));
            tmp_err = tmp_err->child;
        }
        *tmp_err = *err;
        tmp_err->pool = pool;
        if (tmp_err->message)
            tmp_err->message = apr_pstrdup(pool, tmp_err->message);
        if (tmp_err->file)
            tmp_err->file = apr_pstrdup(pool, tmp_err->file);
    }

#if defined(amp_DEBUG)
    apr_pool_cleanup_register(pool, tmp_err,
                              err_abort,
                              apr_pool_cleanup_null);
#endif

    return new_err;
}

void
amp_error_clear(amp_error_t* err)
{
    if (err)
    {
#if defined(amp_DEBUG)
        while (err->child)
            err = err->child;
        apr_pool_cleanup_kill(err->pool, err, err_abort);
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
        amp_error_clear(amp_cmdline_puts(AMP_FILE_LINE_UNDEFINED,
            err->pool));
        amp_error_clear(temp_err);
    }

    {
        const char* symbolic_name;
        if (amp_error__is_tracing_link(err))
            /* Skip it; the error code will be printed by the real link. */
            amp_error_clear(amp_cmdline_printf(err->pool, ",\n"));
        else if ((symbolic_name = amp_error_symbolic_name(err->apr_err)))
            amp_error_clear(amp_cmdline_printf(err->pool,
                ": (apr_err=%s)\n", symbolic_name));
        else
            amp_error_clear(amp_cmdline_printf(err->pool,
                ": (apr_err=%d)\n", err->apr_err));
    }
#endif /* amp_DEBUG */

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
            prefix, err->apr_err, err->message));
    }
    else
    {
        /* Is this a Subversion-specific error code? */
        if ((err->apr_err > APR_OS_START_USEERR)
            && (err->apr_err <= APR_OS_START_CANONERR))
            err_string = amp_strerror(err->apr_err, errbuf, sizeof(errbuf));
        /* Otherwise, this must be an APR error code. */
        else if ((temp_err = amp_cstring_to_utf8
        (&err_string, apr_strerror(err->apr_err, errbuf,
            sizeof(errbuf)), err->pool)))
        {
            amp_error_clear(temp_err);
            err_string = _("Can't recode error string from APR");
        }

        amp_error_clear(amp_cmdline_printf(err->pool,
            "%sE%06d: %s\n",
            prefix, err->apr_err, err_string));
    }
}

void
amp_handle_error2(amp_error_t* err,
                  FILE* stream,
                  amp_boolean_t fatal,
                  const char* prefix)
{
    /* In a long error chain, there may be multiple errors with the same
       error code and no custom message.  We only want to print the
       default message for that code once; printing it multiple times
       would add no useful information.  The 'empties' array below
       remembers the codes of empty errors already seen in the chain.

       We could allocate it in err->pool, but there's no telling how
       long err will live or how many times it will get handled.  So we
       use a subpool. */
    apr_pool_t* subpool;
    apr_array_header_t* empties;
    amp_error_t* tmp_err;

    subpool = amp_pool_create(err->pool);
    empties = apr_array_make(subpool, 0, sizeof(apr_status_t));

    tmp_err = err;
    while (tmp_err)
    {
        amp_boolean_t printed_already = FALSE;

        if (!tmp_err->message)
        {
            int i;

            for (i = 0; i < empties->nelts; i++)
            {
                if (tmp_err->apr_err == APR_ARRAY_IDX(empties, i, apr_status_t))
                {
                    printed_already = TRUE;
                    break;
                }
            }
        }

        if (!printed_already)
        {
            print_error(tmp_err, stream, prefix);
            if (!tmp_err->message)
            {
                APR_ARRAY_PUSH(empties, apr_status_t) = tmp_err->apr_err;
            }
        }

        tmp_err = tmp_err->child;
    }

    amp_pool_destroy(subpool);

    fflush(stream);
    if (fatal)
    {
        /* Avoid abort()s in maintainer mode. */
        amp_error_clear(err);

        /* We exit(1) here instead of abort()ing so that atexit handlers
           get called. */
        exit(EXIT_FAILURE);
    }
}

void
amp_handle_warning2(FILE* stream, const amp_error_t* err, const char* prefix)
{
    char buf[256];
#ifdef amp_DEBUG
    const char* symbolic_name = amp_error_symbolic_name(err->apr_err);
#endif

#ifdef amp_DEBUG
    if (symbolic_name)
        amp_error_clear(
          amp_cmdline_fprintf(stream, err->pool, "%swarning: apr_err=%s\n",
              prefix, symbolic_name));
#endif

    amp_error_clear(amp_cmdline_printf
    (err->pool,
        _("%swarning: W%06d: %s\n"),
        prefix, err->apr_err,
        amp_err_best_message(err, buf, sizeof(buf))));
    fflush(stream);
}

const char*
amp_err_best_message(const amp_error_t* err, char* buf, apr_size_t bufsize)
{
    /* Skip over any trace records.  */
    while (amp_error__is_tracing_link(err))
        err = err->child;
    if (err->message)
        return err->message;
    else
        return amp_strerror(err->apr_err, buf, bufsize);
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
amp_strerror(apr_status_t statcode, char* buf, apr_size_t bufsize)
{
    const err_defn* defn;

    for (defn = error_table; defn->errdesc != NULL; ++defn)
        if (defn->errcode == (amp_errno_t)statcode)
        {
            apr_cpystrn(buf, _(defn->errdesc), bufsize);
            return buf;
        }

    return apr_strerror(statcode, buf, bufsize);
}

#ifdef amp_DEBUG
/* Defines amp__errno and amp__apr_errno */
#include "errorcode.inc"
#endif

const char*
amp_error_symbolic_name(apr_status_t statcode)
{
    const err_defn* defn;
#ifdef amp_DEBUG
    int i;
#endif /* amp_DEBUG */

    for (defn = error_table; defn->errdesc != NULL; ++defn)
        if (defn->errcode == (amp_errno_t)statcode)
            return defn->errname;

    /* "No error" is not in error_table. */
    if (statcode == APR_SUCCESS)
        return "AMP_NO_ERROR";

#ifdef amp_DEBUG
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
#endif /* amp_DEBUG */

    /* ### TODO: do we need APR_* error macros?  What about APR_TO_OS_ERROR()? */

    return NULL;
}



/* Malfunctions. */

amp_error_t*
amp_error_raise_on_malfunction(amp_boolean_t can_return,
                               const char* file, int line,
                               const char* expr)
{
    if (!can_return)
        abort(); /* Nothing else we can do as a library */

      /* The filename and line number of the error source needs to be set
         here because amp_error_createf() is not the macro defined in
         amp_error.h but the real function. */
    amp_error__locate(file, line);

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
                               const char* file, int line,
                               const char* expr)
{
    amp_error_t* err = amp_error_raise_on_malfunction(TRUE, file, line, expr);

    amp_handle_error2(err, stderr, FALSE, "svn: ");
    abort();
    return err;  /* Not reached. */
}


/* Misc. */

amp_error_t*
amp_error__wrap_zlib(int zerr, const char* function, const char* message)
{
    apr_status_t status;
    const char* zmsg;

    if (zerr == Z_OK)
        return AMP_NO_ERROR;

    switch (zerr)
    {
    case Z_STREAM_ERROR:
        status = AMP_ERR_STREAM_MALFORMED_DATA;
        zmsg = _("stream error");
        break;

    case Z_MEM_ERROR:
        status = APR_ENOMEM;
        zmsg = _("out of memory");
        break;

    case Z_BUF_ERROR:
        status = APR_ENOMEM;
        zmsg = _("buffer error");
        break;

    case Z_VERSION_ERROR:
        status = AMP_ERR_STREAM_UNRECOGNIZED_DATA;
        zmsg = _("version error");
        break;

    case Z_DATA_ERROR:
        status = AMP_ERR_STREAM_MALFORMED_DATA;
        zmsg = _("corrupt data");
        break;

    default:
        status = AMP_ERR_STREAM_UNRECOGNIZED_DATA;
        zmsg = _("unknown error");
        break;
    }

    if (message != NULL)
        return amp_error_createf(status, NULL, "zlib (%s): %s: %s", function,
                                 zmsg, message);
    else
        return amp_error_createf(status, NULL, "zlib (%s): %s", function, zmsg);
}
