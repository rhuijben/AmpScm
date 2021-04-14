#include "amp_types.h"

#pragma once

AMP_C__START

#define AMP_SUCCESS 0
#define AMP_NO_ERROR 0

typedef struct amp_error_t amp_error_t;


/* For the AmpScm development mode, this #define turns on extended "stack
   traces" of any errors that get thrown. See the AMP_ERR() macro.  */
#ifdef _DEBUG
#define AMP_ERR__TRACING
#endif

	   /** the best kind of (@c amp_error_t *) ! */
#define AMP_NO_ERROR   0

/* The actual error codes are kept in a separate file; see comments
   there for the reasons why. */
#include "amp_error_codes.h"

	 /** Create a nested exception structure.
	  *
	  * Input:  an APR or AMP custom error code,
	  *         a "child" error to wrap,
	  *         a specific message
	  *
	  * Returns:  a new error structure (containing the old one).
	  *
	  * @note Errors are always allocated in a subpool of the global pool,
	  *        since an error's lifetime is generally not related to the
	  *        lifetime of any convenient pool.  Errors must be freed
	  *        with amp_error_clear().  The specific message should be @c NULL
	  *        if there is nothing to add to the general message associated
	  *        with the error code.
	  *
	  *        If creating the "bottommost" error in a chain, pass @c NULL for
	  *        the child argument.
	  */
AMP_DECLARE(amp_error_t *)
amp_error_create(amp_status_t status,
				 amp_error_t *child,
				 const char *message);

		 /** Create an error structure with the given @a apr_err and @a child,
		  * with a printf-style error message produced by passing @a fmt, using
		  * apr_psprintf().
		  */
AMP_DECLARE(amp_error_t *)
amp_error_createf(amp_status_t status,
				  amp_error_t *child,
				  const char *fmt,
				  ...);

/** If @a child is AMP_NO_ERROR, return AMP_NO_ERROR.
 * Else, prepend a new error to the error chain of @a child. The new error
 * uses @a new_msg as error message but all other error attributes (such
 * as the error code) are copied from @a child.
 */
AMP_DECLARE(amp_error_t *)
amp_error_quick_wrap(amp_error_t *child,
					 const char *new_msg);

/** Like amp_error_quick_wrap(), but with format string support.
 *
 * @since New in 1.9.
 */
amp_error_t *
amp_error_quick_wrapf(amp_error_t *child,
					  const char *fmt,
					  ...)
	__attribute__((format(printf, 2, 3)));

/** Compose two errors, returning the composition as a brand new error
 * and consuming the original errors.  Either or both of @a err1 and
 * @a err2 may be @c AMP_NO_ERROR.  If both are not @c AMP_NO_ERROR,
 * @a err2 will follow @a err1 in the chain of the returned error.
 *
 * Either @a err1 or @a err2 can be functions that return amp_error_t*
 * but if both are functions they can be evaluated in either order as
 * per the C language rules.
 *
 * @since New in 1.6.
 */
AMP_DECLARE(amp_error_t *)
amp_error_compose_create(amp_error_t *err1,
						 amp_error_t *err2);

				 /** Return the root cause of @a err by finding the last error in its
				  * chain (e.g. it or its children).  @a err may be @c AMP_NO_ERROR, in
				  * which case @c AMP_NO_ERROR is returned.  The returned error should
				  * @em not be cleared as it shares memory with @a err.
				  *
				  * @since New in 1.5.
				  */
AMP_DECLARE(amp_error_t *)
amp_error_root_cause(amp_error_t *err);

/** Return the first error in @a err's chain that has an error code @a
 * apr_err or #AMP_NO_ERROR if there is no error with that code.  The
 * returned error should @em not be cleared as it shares memory with @a err.
 *
 * If @a err is #AMP_NO_ERROR, return #AMP_NO_ERROR.
 *
 * @since New in 1.7.
 */
AMP_DECLARE(amp_error_t *)
amp_error_find_cause(amp_error_t *err, apr_status_t apr_err);

/** Create a new error that is a deep copy of @a err and return it.
 *
 * @since New in 1.2.
 */
AMP_DECLARE(amp_error_t *)
amp_error_dup(const amp_error_t *err);

/** Free the memory used by @a error, as well as all ancestors and
 * descendants of @a error.
 *
 * Unlike other Subversion objects, errors are managed explicitly; you
 * MUST clear an error if you are ignoring it, or you are leaking memory.
 * For convenience, @a error may be @c NULL, in which case this function does
 * nothing; thus, amp_error_clear(amp_foo(...)) works as an idiom to
 * ignore errors.
 */
void
amp_error_clear(amp_error_t *error);


#if defined(AMP_ERR__TRACING)
	/** Set the error location for debug mode. */
AMP_DECLARE(void)
amp_error__locate(const char *file,
				  long line,
				  const char *function);

		  /* Wrapper macros to collect file and line information */
#define amp_error_create \
  (amp_error__locate(__FILE__,__LINE__,__func__), (amp_error_create))
#define amp_error_createf \
  (amp_error__locate(__FILE__,__LINE__,__func__), (amp_error_createf))
#define amp_error_wrap_apr \
  (amp_error__locate(__FILE__,__LINE__,__func__), (amp_error_wrap_apr))
#define amp_error_quick_wrap \
  (amp_error__locate(__FILE__,__LINE__,__func__), (amp_error_quick_wrap))
#define amp_error_quick_wrapf \
  (amp_error__locate(__FILE__,__LINE__,__func__), (amp_error_quick_wrapf))
#endif

	/** A statement macro for checking error values.
	 *
	 * Evaluate @a expr.  If it yields an error, return that error from the
	 * current function.  Otherwise, continue.
	 *
	 * The <tt>do { ... } while (0)</tt> wrapper has no semantic effect,
	 * but it makes this macro syntactically equivalent to the expression
	 * statement it resembles.  Without it, statements like
	 *
	 * @code
	 *   if (a)
	 *     AMP_ERR(some operation);
	 *   else
	 *     foo;
	 * @endcode
	 *
	 * would not mean what they appear to.
	 */
#define AMP_ERR(expr)                           \
  do {                                          \
    amp_error_t *amp_err__temp = (expr);        \
    if (amp_err__temp)                          \
      return amp_error_trace(amp_err__temp);    \
  } while (0)

	 /**
	  * A macro for wrapping an error in a source-location trace message.
	  *
	  * This macro can be used when directly returning an already created
	  * error (when not using AMP_ERR, amp_error_create(), etc.) to ensure
	  * that the call stack is recorded correctly.
	  *
	  * @since New in 1.7.
	  */
#ifdef AMP_ERR__TRACING
amp_error_t *
amp_error__trace(const char *file, long line, const char *function, amp_error_t *err);

#define amp_error_trace(expr)  amp_error__trace(__FILE__, __LINE__, __FUNCTION__, (expr))
#else
#define amp_error_trace(expr)  (expr)
#endif

/** A statement macro, very similar to @c AMP_ERR.
 *
 * This macro will wrap the error with the specified text before
 * returning the error.
 */
#define AMP_ERR_W(expr, wrap_msg)                           \
  do {                                                      \
    amp_error_t *amp_err__temp = (expr);                    \
    if (amp_err__temp)                                      \
      return amp_error_quick_wrap(amp_err__temp, wrap_msg); \
  } while (0)


AMP_DECLARE(char*)
amp_strerror(amp_status_t statcode, char* buf, apr_size_t bufsize);


AMP_C__END