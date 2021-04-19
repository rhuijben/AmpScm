/* What's going on here?

   In order to define error codes and their associated description
   strings in the same place, we overload the AMP_ERRDEF() macro with
   two definitions below.  Both take two arguments, an error code name
   and a description string.  One definition of the macro just throws
   away the string and defines enumeration constants using the error
   code names -- that definition is used by the header file that
   exports error codes to the rest of AmpScm.  The other
   definition creates a static table mapping the enum codes to their
   corresponding strings -- that definition is used by the C file that
   implements amp_strerror().

   The header and C files both include this file, using #defines to
   control which version of the macro they get.
*/


/* Process this file if we're building an error array, or if we have
   not defined the enumerated constants yet.  */
#if defined(AMP_ERROR_BUILD_ARRAY) || !defined(AMP_ERROR_ENUM_DEFINED)

   /* Note: despite lacking double underscores in its name, the macro
	  AMP_ERROR_BUILD_ARRAY is an implementation detail of AmpScm and not
	  a public API. */


#include "amp_errno.h"

AMP_C__START

#ifndef DOXYGEN_SHOULD_SKIP_THIS

#if defined(AMP_ERROR_BUILD_ARRAY)

#define AMP_ERROR_START \
        static const err_defn error_table[] = { \
          { AMP_WARNING, "AMP_WARNING", "Warning" },
#define AMP_ERRDEF(num, offset, str) { num, #num, str },
#define AMP_ERR_MAPPED(num, str) { (amp_status_t)num, #num, str },
#define AMP_ERROR_END { (amp_status_t)0, NULL, NULL } };

#elif !defined(AMP_ERROR_ENUM_DEFINED)

#define AMP_ERROR_START \
        typedef enum amp_errno_t { \
          AMP_WARNING = AMP_OS_START_USERERR + 1,
#define AMP_ERRDEF(num, offset, str) /** str */ num = offset,
#define AMP_ERR_MAPPED(num, str)
#define AMP_ERROR_END AMP_ERR_LAST } svn_errno_t;

#define AMP_ERROR_ENUM_DEFINED

#endif

	/* Define custom Amp error numbers, in the range reserved for
	   that in APR: from AMP_OS_START_USERERR to AMP_OS_START_SYSERR (see
	   apr_errno.h).

	   Error numbers are divided into categories of up to 5000 errors
	   each.  Since we're dividing up the APR user error space, which has
	   room for 500,000 errors, we can have up to 100 categories.
	   Categories are fixed-size; if a category has fewer than 5000
	   errors, then it just ends with a range of unused numbers.

	   To maintain binary compatibility, please observe these guidelines:

		  - When adding a new error, always add on the end of the
			appropriate category, so that the real values of existing
			errors are not changed.

		  - When deleting an error, leave a placeholder comment indicating
			the offset, again so that the values of other errors are not
			perturbed.
	*/

#define AMP_ERR_CATEGORY_SIZE 5000
#define SVN_ERR_OFFSET AMP_ERR_CATEGORY_SIZE * 40

	/* Leave one category of room at the beginning, for AMP_WARNING and
	   any other such beasts we might create in the future. */
#define AMP_ERR_BAD_CATEGORY_START      (AMP_OS_START_USERERR + SVN_ERR_OFFSET\
                                         + ( 1 * AMP_ERR_CATEGORY_SIZE))
#define AMP_ERR_XML_CATEGORY_START      (AMP_OS_START_USERERR + SVN_ERR_OFFSET\
                                         + ( 2 * AMP_ERR_CATEGORY_SIZE))
#define AMP_ERR_IO_CATEGORY_START       (AMP_OS_START_USERERR + SVN_ERR_OFFSET\
                                         + ( 3 * AMP_ERR_CATEGORY_SIZE))
#define AMP_ERR_STREAM_CATEGORY_START   (AMP_OS_START_USERERR + SVN_ERR_OFFSET\
                                         + ( 4 * AMP_ERR_CATEGORY_SIZE))


#endif /* DOXYGEN_SHOULD_SKIP_THIS */

	   /** Collection of Subversion error code values, located within the
		* APR user error space. */
	AMP_ERROR_START

		/* validation ("BAD_FOO") errors */

		AMP_ERRDEF(AMP_ERR_BAD_CONTAINING_POOL,
				   AMP_ERR_BAD_CATEGORY_START + 0,
				   "Bad parent pool passed to svn_make_pool()")

		AMP_ERRDEF(AMP_ERR_BAD_FILENAME,
				   AMP_ERR_BAD_CATEGORY_START + 1,
				   "Bogus filename")

		AMP_ERRDEF(AMP_ERR_BAD_URL,
				   AMP_ERR_BAD_CATEGORY_START + 2,
				   "Bogus URL")

		AMP_ERRDEF(AMP_ERR_BAD_DATE,
				   AMP_ERR_BAD_CATEGORY_START + 3,
				   "Bogus date")

		AMP_ERRDEF(AMP_ERR_BAD_MIME_TYPE,
				   AMP_ERR_BAD_CATEGORY_START + 4,
				   "Bogus mime-type")

		AMP_ERRDEF(AMP_ERR_BAD_PROPERTY_VALUE,
				   AMP_ERR_BAD_CATEGORY_START + 5,
				   "Wrong or unexpected property value")

		AMP_ERRDEF(AMP_ERR_BAD_VERSION_FILE_FORMAT,
				   AMP_ERR_BAD_CATEGORY_START + 6,
				   "Version file format not correct")

		AMP_ERRDEF(AMP_ERR_BAD_RELATIVE_PATH,
				   AMP_ERR_BAD_CATEGORY_START + 7,
				   "Path is not an immediate child of the specified directory")

		AMP_ERRDEF(AMP_ERR_BAD_UUID,
				   AMP_ERR_BAD_CATEGORY_START + 8,
				   "Bogus UUID")


		AMP_ERRDEF(AMP_ERR_BAD_CONFIG_VALUE,
				   AMP_ERR_BAD_CATEGORY_START + 9,
				   "Invalid configuration value")

		AMP_ERRDEF(AMP_ERR_BAD_SERVER_SPECIFICATION,
				   AMP_ERR_BAD_CATEGORY_START + 10,
				   "Bogus server specification")

		AMP_ERRDEF(AMP_ERR_BAD_CHECKSUM_KIND,
				   AMP_ERR_BAD_CATEGORY_START + 11,
				   "Unsupported checksum type")

		AMP_ERRDEF(AMP_ERR_BAD_CHECKSUM_PARSE,
				   AMP_ERR_BAD_CATEGORY_START + 12,
				   "Invalid character in hex checksum")


		AMP_ERRDEF(AMP_ERR_BAD_TOKEN,
				   AMP_ERR_BAD_CATEGORY_START + 13,
				   "Unknown string value of token")


		AMP_ERRDEF(AMP_ERR_BAD_CHANGELIST_NAME,
				   AMP_ERR_BAD_CATEGORY_START + 14,
				   "Invalid changelist name")


		AMP_ERRDEF(AMP_ERR_BAD_ATOMIC,
				   AMP_ERR_BAD_CATEGORY_START + 15,
				   "Invalid atomic")


		AMP_ERRDEF(AMP_ERR_BAD_COMPRESSION_METHOD,
				   AMP_ERR_BAD_CATEGORY_START + 16,
				   "Invalid compression method")


		AMP_ERRDEF(AMP_ERR_BAD_PROPERTY_VALUE_EOL,
				   AMP_ERR_BAD_CATEGORY_START + 17,
				   "Unexpected line ending in the property value")

		AMP_ERRDEF(AMP_ERR_NOT_IMPLEMENTED,
				   AMP_ERR_BAD_CATEGORY_START + 18,
				   "Not implemented")

		AMP_ERRDEF(AMP_ERR_COMPOSED_ERROR,
				   AMP_ERR_BAD_CATEGORY_START + 19,
				   "Additional errors:")

		AMP_ERRDEF(AMP_ERR_MALFUNCTION,
				   AMP_ERR_BAD_CATEGORY_START + 20,
				   "General malfunction")

		AMP_ERRDEF(AMP_ERR_ASSERTION_FAIL,
				   AMP_ERR_BAD_CATEGORY_START + 21,
				   "Assertion failed")

		AMP_ERRDEF(AMP_ERR_NOT_SUPPORTED,
				   AMP_ERR_BAD_CATEGORY_START + 22,
				   "Feature not supported")

		/* xml errors */

		AMP_ERRDEF(AMP_ERR_XML_ATTRIB_NOT_FOUND,
				   AMP_ERR_XML_CATEGORY_START + 0,
				   "No such XML tag attribute")

		AMP_ERRDEF(AMP_ERR_XML_MISSING_ANCESTRY,
				   AMP_ERR_XML_CATEGORY_START + 1,
				   "<delta-pkg> is missing ancestry")

		AMP_ERRDEF(AMP_ERR_XML_UNKNOWN_ENCODING,
				   AMP_ERR_XML_CATEGORY_START + 2,
				   "Unrecognized binary data encoding; can't decode")

		AMP_ERRDEF(AMP_ERR_XML_MALFORMED,
				   AMP_ERR_XML_CATEGORY_START + 3,
				   "XML data was not well-formed")

		AMP_ERRDEF(AMP_ERR_XML_UNESCAPABLE_DATA,
				   AMP_ERR_XML_CATEGORY_START + 4,
				   "Data cannot be safely XML-escaped")


		AMP_ERRDEF(AMP_ERR_XML_UNEXPECTED_ELEMENT,
				   AMP_ERR_XML_CATEGORY_START + 5,
				   "Unexpected XML element found")

		/* io errors */

		AMP_ERRDEF(AMP_ERR_IO_INCONSISTENT_EOL,
				   AMP_ERR_IO_CATEGORY_START + 0,
				   "Inconsistent line ending style")

		AMP_ERRDEF(AMP_ERR_IO_UNKNOWN_EOL,
				   AMP_ERR_IO_CATEGORY_START + 1,
				   "Unrecognized line ending style")

		/** @deprecated Unused, slated for removal in the next major release. */
		AMP_ERRDEF(AMP_ERR_IO_CORRUPT_EOL,
				   AMP_ERR_IO_CATEGORY_START + 2,
				   "Line endings other than expected")

		AMP_ERRDEF(AMP_ERR_IO_UNIQUE_NAMES_EXHAUSTED,
				   AMP_ERR_IO_CATEGORY_START + 3,
				   "Ran out of unique names")

		/** @deprecated Unused, slated for removal in the next major release. */
		AMP_ERRDEF(AMP_ERR_IO_PIPE_FRAME_ERROR,
				   AMP_ERR_IO_CATEGORY_START + 4,
				   "Framing error in pipe protocol")

		/** @deprecated Unused, slated for removal in the next major release. */
		AMP_ERRDEF(AMP_ERR_IO_PIPE_READ_ERROR,
				   AMP_ERR_IO_CATEGORY_START + 5,
				   "Read error in pipe")

		AMP_ERRDEF(AMP_ERR_IO_WRITE_ERROR,
				   AMP_ERR_IO_CATEGORY_START + 6,
				   "Write error")


		AMP_ERRDEF(AMP_ERR_IO_PIPE_WRITE_ERROR,
				   AMP_ERR_IO_CATEGORY_START + 7,
				   "Write error in pipe")

		/* stream errors */

		AMP_ERRDEF(AMP_ERR_STREAM_UNEXPECTED_EOF,
				   AMP_ERR_STREAM_CATEGORY_START + 0,
				   "Unexpected EOF on stream")

		AMP_ERRDEF(AMP_ERR_STREAM_MALFORMED_DATA,
				   AMP_ERR_STREAM_CATEGORY_START + 1,
				   "Malformed stream data")

		AMP_ERRDEF(AMP_ERR_STREAM_UNRECOGNIZED_DATA,
				   AMP_ERR_STREAM_CATEGORY_START + 2,
				   "Unrecognized stream data")


		AMP_ERRDEF(AMP_ERR_STREAM_SEEK_NOT_SUPPORTED,
				   AMP_ERR_STREAM_CATEGORY_START + 3,
				   "Stream doesn't support seeking")

		AMP_ERRDEF(AMP_ERR_STREAM_NOT_SUPPORTED,
				   AMP_ERR_STREAM_CATEGORY_START + 4,
				   "Stream doesn't support this capability")

		AMP_ERR_MAPPED(AMP_ENOSTAT, "Could not perform a stat on the file.")
		AMP_ERR_MAPPED(AMP_ENOPOOL, "A new pool could not be created.")
		AMP_ERR_MAPPED(AMP_EBADDATE, "An invalid date has been provided")
		AMP_ERR_MAPPED(AMP_EINVALSOCK, "An invalid socket was returned")
		AMP_ERR_MAPPED(AMP_ENOPROC, "No process was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOTIME, "No time was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENODIR, "No directory was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOLOCK, "No lock was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOPOLL, "No poll structure was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOSOCKET, "No socket was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOTHREAD, "No thread was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOTHDKEY, "No thread key structure was provided and one was required.")
		AMP_ERR_MAPPED(AMP_ENOSHMAVAIL, "No shared memory is currently available")
		AMP_ERR_MAPPED(AMP_EDSOOPEN, "DSO load failed")
		AMP_ERR_MAPPED(AMP_EBADIP, "The specified IP address is invalid.")
		AMP_ERR_MAPPED(AMP_EBADMASK, "The specified network mask is invalid.")
		AMP_ERR_MAPPED(AMP_ESYMNOTFOUND, "Could not find the requested symbol.")
		AMP_ERR_MAPPED(AMP_ENOTENOUGHENTROPY, "Not enough entropy to continue.")
		AMP_ERR_MAPPED(AMP_INCHILD, "Your code just forked, and you are currently executing in the "
				"child process")
		AMP_ERR_MAPPED(AMP_INPARENT, "Your code just forked, and you are currently executing in the "
				"parent process")
		AMP_ERR_MAPPED(AMP_DETACH, "The specified thread is detached")
		AMP_ERR_MAPPED(AMP_NOTDETACH, "The specified thread is not detached")
		AMP_ERR_MAPPED(AMP_CHILD_DONE, "The specified child process is done executing")
		AMP_ERR_MAPPED(AMP_CHILD_NOTDONE, "The specified child process is not done executing")
		AMP_ERR_MAPPED(AMP_TIMEUP, "The timeout specified has expired")
		AMP_ERR_MAPPED(AMP_INCOMPLETE, "Partial results are valid but processing is incomplete")
		AMP_ERR_MAPPED(AMP_BADCH, "Bad character specified on command line")
		AMP_ERR_MAPPED(AMP_BADARG, "Missing parameter for the specified command line option")
		AMP_ERR_MAPPED(AMP_EOF, "End of file found")
		AMP_ERR_MAPPED(AMP_NOTFOUND, "Could not find specified socket in poll list.")
		AMP_ERR_MAPPED(AMP_ANONYMOUS, "Shared memory is implemented anonymously")
		AMP_ERR_MAPPED(AMP_FILEBASED, "Shared memory is implemented using files")
		AMP_ERR_MAPPED(AMP_KEYBASED, "Shared memory is implemented using a key system")
		AMP_ERR_MAPPED(AMP_EINIT, "There is no error, this value signifies an initialized "
				"error code")
		AMP_ERR_MAPPED(AMP_ENOTIMPL, "This function has not been implemented on this platform")
		AMP_ERR_MAPPED(AMP_EMISMATCH, "passwords do not match")
		AMP_ERR_MAPPED(AMP_EABSOLUTE, "The given path is absolute")
		AMP_ERR_MAPPED(AMP_ERELATIVE, "The given path is relative")
		AMP_ERR_MAPPED(AMP_EINCOMPLETE, "The given path is incomplete")
		AMP_ERR_MAPPED(AMP_EABOVEROOT, "The given path was above the root path")
		AMP_ERR_MAPPED(AMP_EBADPATH, "The given path is misformatted or contained invalid characters")
		AMP_ERR_MAPPED(AMP_EPATHWILD, "The given path contained wildcard characters")
		AMP_ERR_MAPPED(AMP_EBUSY, "The given lock was busy.")
		AMP_ERR_MAPPED(AMP_EPROC_UNKNOWN, "The process is not recognized.")
		AMP_ERR_MAPPED(AMP_EGENERAL, "Internal error (specific information not available)")


		AMP_ERROR_END


#undef AMP_ERROR_START
#undef AMP_ERRDEF
#undef AMP_ERROR_END

AMP_C__END

#endif /* defined(AMP_ERROR_BUILD_ARRAY) || !defined(AMP_ERROR_ENUM_DEFINED) */
