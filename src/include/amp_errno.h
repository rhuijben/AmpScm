/** AmpSCM error code definition
 *
 * AmpScm error numbers are designed to contain both APR and OS error numbers on Windows and *nix,
 * while having space for other error ranges.
 */

 /* Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#pragma once
#include "amp_types.h"

#include <errno.h>
#ifdef _WIN32
#include <winerror.h>
#endif
/* The best error */
#define AMP_SUCCESS (int)0

AMP_C__START


/**
* @defgroup amp_errno Error Codes
* @ingroup APR
* @{
*/

/**
* AMP_OS_START_ERROR is where the APR specific error values start.
*/
#define AMP_OS_START_ERROR     20000
/**
* AMP_OS_ERRSPACE_SIZE is the maximum number of errors you can fit
*    into one of the error/status ranges below -- except for
*    AMP_OS_START_USERERR, which see.
*/
#define AMP_OS_ERRSPACE_SIZE 50000
/**
* AMP_UTIL_ERRSPACE_SIZE is the size of the space that is reserved for
* use within apr-util. This space is reserved above that used by APR
* internally.
* @note This number MUST be smaller than AMP_OS_ERRSPACE_SIZE by a
*       large enough amount that APR has sufficient room for its
*       codes.
*/
#define AMP_UTIL_ERRSPACE_SIZE 20000
/**
* AMP_OS_START_STATUS is where the APR specific status codes start.
*/
#define AMP_OS_START_STATUS    (AMP_OS_START_ERROR + AMP_OS_ERRSPACE_SIZE)
/**
* AMP_UTIL_START_STATUS is where APR-Util starts defining its
* status codes.
*/
#define AMP_UTIL_START_STATUS   (AMP_OS_START_STATUS + \
                           (AMP_OS_ERRSPACE_SIZE - AMP_UTIL_ERRSPACE_SIZE))
/**
* AMP_OS_START_USERERR are reserved for applications that use APR that
*     layer their own error codes along with APR's.  Note that the
*     error immediately following this one is set ten times farther
*     away than usual, so that users of apr have a lot of room in
*     which to declare custom error codes.
*
* In general applications should try and create unique error codes. To try
* and assist in finding suitable ranges of numbers to use, the following
* ranges are known to be used by the listed applications. If your
* application defines error codes please advise the range of numbers it
* uses to dev@apr.apache.org for inclusion in this list.
*
* Ranges shown are in relation to AMP_OS_START_USERERR
*
* Subversion - Defined ranges, of less than 100, at intervals of 5000
*              starting at an offset of 5000, e.g.
*               +5000 to 5100,  +10000 to 10100
*
* Apache HTTPD - +2000 to 2999
*/
#define AMP_OS_START_USERERR    (AMP_OS_START_STATUS + AMP_OS_ERRSPACE_SIZE)
/**
* AMP_OS_START_USEERR is obsolete, defined for compatibility only.
* Use AMP_OS_START_USERERR instead.
*/
#define AMP_OS_START_USEERR     AMP_OS_START_USERERR
/**
* AMP_OS_START_CANONERR is where APR versions of errno values are defined
*     on systems which don't have the corresponding errno.
*/
#define AMP_OS_START_CANONERR  (AMP_OS_START_USERERR \
                                 + (AMP_OS_ERRSPACE_SIZE * 10))
/**
* AMP_OS_START_EAIERR folds EAI_ error codes from getaddrinfo() into
*     amp_status_t values.
*/
#define AMP_OS_START_EAIERR    (AMP_OS_START_CANONERR + AMP_OS_ERRSPACE_SIZE)
/**
* AMP_OS_START_SYSERR folds platform-specific system error values into
*     amp_status_t values.
*/
#define AMP_OS_START_SYSERR    (AMP_OS_START_EAIERR + AMP_OS_ERRSPACE_SIZE)

/**
* @defgroup AMP_ERR_map APR Error Space
* <PRE>
* The following attempts to show the relation of the various constants
* used for mapping APR Status codes.
*
*       0
*
*  20,000     AMP_OS_START_ERROR
*
*         + AMP_OS_ERRSPACE_SIZE (50,000)
*
*  70,000      AMP_OS_START_STATUS
*
*         + AMP_OS_ERRSPACE_SIZE - AMP_UTIL_ERRSPACE_SIZE (30,000)
*
* 100,000      AMP_UTIL_START_STATUS
*
*         + AMP_UTIL_ERRSPACE_SIZE (20,000)
*
* 120,000      AMP_OS_START_USERERR
*
*         + 10 x AMP_OS_ERRSPACE_SIZE (50,000 * 10)
*
* 620,000      AMP_OS_START_CANONERR
*
*         + AMP_OS_ERRSPACE_SIZE (50,000)
*
* 670,000      AMP_OS_START_EAIERR
*
*         + AMP_OS_ERRSPACE_SIZE (50,000)
*
* 720,000      AMP_OS_START_SYSERR
*
* </PRE>
*/

/** no error. */

/**
* @defgroup AMP_Error APR Error Values
* <PRE>
* <b>APR ERROR VALUES</b>
* AMP_ENOSTAT      APR was unable to perform a stat on the file
* AMP_ENOPOOL      APR was not provided a pool with which to allocate memory
* AMP_EBADDATE     APR was given an invalid date
* AMP_EINVALSOCK   APR was given an invalid socket
* AMP_ENOPROC      APR was not given a process structure
* AMP_ENOTIME      APR was not given a time structure
* AMP_ENODIR       APR was not given a directory structure
* AMP_ENOLOCK      APR was not given a lock structure
* AMP_ENOPOLL      APR was not given a poll structure
* AMP_ENOSOCKET    APR was not given a socket
* AMP_ENOTHREAD    APR was not given a thread structure
* AMP_ENOTHDKEY    APR was not given a thread key structure
* AMP_ENOSHMAVAIL  There is no more shared memory available
* AMP_EDSOOPEN     APR was unable to open the dso object.  For more
*                  information call amp_dso_error().
* AMP_EGENERAL     General failure (specific information not available)
* AMP_EBADIP       The specified IP address is invalid
* AMP_EBADMASK     The specified netmask is invalid
* AMP_ESYMNOTFOUND Could not find the requested symbol
* AMP_ENOTENOUGHENTROPY Not enough entropy to continue
* </PRE>
*
* <PRE>
* <b>APR STATUS VALUES</b>
* AMP_INCHILD        Program is currently executing in the child
* AMP_INPARENT       Program is currently executing in the parent
* AMP_DETACH         The thread is detached
* AMP_NOTDETACH      The thread is not detached
* AMP_CHILD_DONE     The child has finished executing
* AMP_CHILD_NOTDONE  The child has not finished executing
* AMP_TIMEUP         The operation did not finish before the timeout
* AMP_INCOMPLETE     The operation was incomplete although some processing
*                    was performed and the results are partially valid
* AMP_BADCH          Getopt found an option not in the option string
* AMP_BADARG         Getopt found an option that is missing an argument
*                    and an argument was specified in the option string
* AMP_EOF            APR has encountered the end of the file
* AMP_NOTFOUND       APR was unable to find the socket in the poll structure
* AMP_ANONYMOUS      APR is using anonymous shared memory
* AMP_FILEBASED      APR is using a file name as the key to the shared memory
* AMP_KEYBASED       APR is using a shared key as the key to the shared memory
* AMP_EINIT          Ininitalizer value.  If no option has been found, but
*                    the status variable requires a value, this should be used
* AMP_ENOTIMPL       The APR function has not been implemented on this
*                    platform, either because nobody has gotten to it yet,
*                    or the function is impossible on this platform.
* AMP_EMISMATCH      Two passwords do not match.
* AMP_EABSOLUTE      The given path was absolute.
* AMP_ERELATIVE      The given path was relative.
* AMP_EINCOMPLETE    The given path was neither relative nor absolute.
* AMP_EABOVEROOT     The given path was above the root path.
* AMP_EBUSY          The given lock was busy.
* AMP_EPROC_UNKNOWN  The given process wasn't recognized by APR
* </PRE>
* @{
*/
/** @see AMP_ERR_IS_ENOSTAT */
#define AMP_ENOSTAT        (AMP_OS_START_ERROR + 1)
/** @see AMP_ERR_IS_ENOPOOL */
#define AMP_ENOPOOL        (AMP_OS_START_ERROR + 2)
/* empty slot: +3 */
/** @see AMP_ERR_IS_EBADDATE */
#define AMP_EBADDATE       (AMP_OS_START_ERROR + 4)
/** @see AMP_ERR_IS_EINVALSOCK */
#define AMP_EINVALSOCK     (AMP_OS_START_ERROR + 5)
/** @see AMP_ERR_IS_ENOPROC */
#define AMP_ENOPROC        (AMP_OS_START_ERROR + 6)
/** @see AMP_ERR_IS_ENOTIME */
#define AMP_ENOTIME        (AMP_OS_START_ERROR + 7)
/** @see AMP_ERR_IS_ENODIR */
#define AMP_ENODIR         (AMP_OS_START_ERROR + 8)
/** @see AMP_ERR_IS_ENOLOCK */
#define AMP_ENOLOCK        (AMP_OS_START_ERROR + 9)
/** @see AMP_ERR_IS_ENOPOLL */
#define AMP_ENOPOLL        (AMP_OS_START_ERROR + 10)
/** @see AMP_ERR_IS_ENOSOCKET */
#define AMP_ENOSOCKET      (AMP_OS_START_ERROR + 11)
/** @see AMP_ERR_IS_ENOTHREAD */
#define AMP_ENOTHREAD      (AMP_OS_START_ERROR + 12)
/** @see AMP_ERR_IS_ENOTHDKEY */
#define AMP_ENOTHDKEY      (AMP_OS_START_ERROR + 13)
/** @see AMP_ERR_IS_EGENERAL */
#define AMP_EGENERAL       (AMP_OS_START_ERROR + 14)
/** @see AMP_ERR_IS_ENOSHMAVAIL */
#define AMP_ENOSHMAVAIL    (AMP_OS_START_ERROR + 15)
/** @see AMP_ERR_IS_EBADIP */
#define AMP_EBADIP         (AMP_OS_START_ERROR + 16)
/** @see AMP_ERR_IS_EBADMASK */
#define AMP_EBADMASK       (AMP_OS_START_ERROR + 17)
/* empty slot: +18 */
/** @see AMP_ERR_IS_EDSOPEN */
#define AMP_EDSOOPEN       (AMP_OS_START_ERROR + 19)
/** @see AMP_ERR_IS_EABSOLUTE */
#define AMP_EABSOLUTE      (AMP_OS_START_ERROR + 20)
/** @see AMP_ERR_IS_ERELATIVE */
#define AMP_ERELATIVE      (AMP_OS_START_ERROR + 21)
/** @see AMP_ERR_IS_EINCOMPLETE */
#define AMP_EINCOMPLETE    (AMP_OS_START_ERROR + 22)
/** @see AMP_ERR_IS_EABOVEROOT */
#define AMP_EABOVEROOT     (AMP_OS_START_ERROR + 23)
/** @see AMP_ERR_IS_EBADPATH */
#define AMP_EBADPATH       (AMP_OS_START_ERROR + 24)
/** @see AMP_ERR_IS_EPATHWILD */
#define AMP_EPATHWILD      (AMP_OS_START_ERROR + 25)
/** @see AMP_ERR_IS_ESYMNOTFOUND */
#define AMP_ESYMNOTFOUND   (AMP_OS_START_ERROR + 26)
/** @see AMP_ERR_IS_EPROC_UNKNOWN */
#define AMP_EPROC_UNKNOWN  (AMP_OS_START_ERROR + 27)
/** @see AMP_ERR_IS_ENOTENOUGHENTROPY */
#define AMP_ENOTENOUGHENTROPY (AMP_OS_START_ERROR + 28)
/** @} */

/**
* @defgroup AMP_STATUS_IS Status Value Tests
* @warning For any particular error condition, more than one of these tests
*      may match. This is because platform-specific error codes may not
*      always match the semantics of the POSIX codes these tests (and the
*      corresponding APR error codes) are named after. A notable example
*      are the AMP_ERR_IS_ENOENT and AMP_ERR_IS_ENOTDIR tests on
*      Win32 platforms. The programmer should always be aware of this and
*      adjust the order of the tests accordingly.
* @{
*/
/**
* APR was unable to perform a stat on the file
* @warning always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_ENOSTAT(e)        ((e)->status == AMP_ENOSTAT)
/**
* APR was not provided a pool with which to allocate memory
* @warning always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_ENOPOOL(e)        ((e)->status == AMP_ENOPOOL)
/** APR was given an invalid date  */
#define AMP_ERR_IS_EBADDATE(e)       ((e)->status == AMP_EBADDATE)
/** APR was given an invalid socket */
#define AMP_ERR_IS_EINVALSOCK(e)     ((e)->status == AMP_EINVALSOCK)
/** APR was not given a process structure */
#define AMP_ERR_IS_ENOPROC(e)        ((e)->status == AMP_ENOPROC)
/** APR was not given a time structure */
#define AMP_ERR_IS_ENOTIME(e)        ((e)->status == AMP_ENOTIME)
/** APR was not given a directory structure */
#define AMP_ERR_IS_ENODIR(e)         ((e)->status == AMP_ENODIR)
/** APR was not given a lock structure */
#define AMP_ERR_IS_ENOLOCK(e)        ((e)->status == AMP_ENOLOCK)
/** APR was not given a poll structure */
#define AMP_ERR_IS_ENOPOLL(e)        ((e)->status == AMP_ENOPOLL)
/** APR was not given a socket */
#define AMP_ERR_IS_ENOSOCKET(e)      ((e)->status == AMP_ENOSOCKET)
/** APR was not given a thread structure */
#define AMP_ERR_IS_ENOTHREAD(e)      ((e)->status == AMP_ENOTHREAD)
/** APR was not given a thread key structure */
#define AMP_ERR_IS_ENOTHDKEY(e)      ((e)->status == AMP_ENOTHDKEY)
/** Generic Error which can not be put into another spot */
#define AMP_ERR_IS_EGENERAL(e)       ((e)->status == AMP_EGENERAL)
/** There is no more shared memory available */
#define AMP_ERR_IS_ENOSHMAVAIL(e)    ((e)->status == AMP_ENOSHMAVAIL)
/** The specified IP address is invalid */
#define AMP_ERR_IS_EBADIP(e)         ((e)->status == AMP_EBADIP)
/** The specified netmask is invalid */
#define AMP_ERR_IS_EBADMASK(e)       ((e)->status == AMP_EBADMASK)
/* empty slot: +18 */
/**
* APR was unable to open the dso object.
* For more information call amp_dso_error().
*/
#if defined(WIN32)
#define AMP_ERR_IS_EDSOOPEN(e)       ((e)->status == AMP_EDSOOPEN \
                       || AMP_ERR_TO_OS(e)->status == ERROR_MOD_NOT_FOUND)
#else
#define AMP_ERR_IS_EDSOOPEN(e)       ((e)->status == AMP_EDSOOPEN)
#endif
/** The given path was absolute. */
#define AMP_ERR_IS_EABSOLUTE(e)      ((e)->status == AMP_EABSOLUTE)
/** The given path was relative. */
#define AMP_ERR_IS_ERELATIVE(e)      ((e)->status == AMP_ERELATIVE)
/** The given path was neither relative nor absolute. */
#define AMP_ERR_IS_EINCOMPLETE(e)    ((e)->status == AMP_EINCOMPLETE)
/** The given path was above the root path. */
#define AMP_ERR_IS_EABOVEROOT(e)     ((e)->status == AMP_EABOVEROOT)
/** The given path was bad. */
#define AMP_ERR_IS_EBADPATH(e)       ((e)->status == AMP_EBADPATH)
/** The given path contained wildcards. */
#define AMP_ERR_IS_EPATHWILD(e)      ((e)->status == AMP_EPATHWILD)
/** Could not find the requested symbol.
* For more information call amp_dso_error().
*/
#if defined(WIN32)
#define AMP_ERR_IS_ESYMNOTFOUND(e)   ((e)->status == AMP_ESYMNOTFOUND \
                       || AMP_ERR_TO_OS(e)->status == ERROR_PROC_NOT_FOUND)
#else
#define AMP_ERR_IS_ESYMNOTFOUND(e)   ((e)->status == AMP_ESYMNOTFOUND)
#endif
/** The given process was not recognized by APR. */
#define AMP_ERR_IS_EPROC_UNKNOWN(e)  ((e)->status == AMP_EPROC_UNKNOWN)
/** APR could not gather enough entropy to continue. */
#define AMP_ERR_IS_ENOTENOUGHENTROPY(e) ((e)->status == AMP_ENOTENOUGHENTROPY)

/** @} */

/**
* @addtogroup AMP_Error
* @{
*/
/** @see AMP_ERR_IS_INCHILD */
#define AMP_INCHILD        (AMP_OS_START_STATUS + 1)
/** @see AMP_ERR_IS_INPARENT */
#define AMP_INPARENT       (AMP_OS_START_STATUS + 2)
/** @see AMP_ERR_IS_DETACH */
#define AMP_DETACH         (AMP_OS_START_STATUS + 3)
/** @see AMP_ERR_IS_NOTDETACH */
#define AMP_NOTDETACH      (AMP_OS_START_STATUS + 4)
/** @see AMP_ERR_IS_CHILD_DONE */
#define AMP_CHILD_DONE     (AMP_OS_START_STATUS + 5)
/** @see AMP_ERR_IS_CHILD_NOTDONE */
#define AMP_CHILD_NOTDONE  (AMP_OS_START_STATUS + 6)
/** @see AMP_ERR_IS_TIMEUP */
#define AMP_TIMEUP         (AMP_OS_START_STATUS + 7)
/** @see AMP_ERR_IS_INCOMPLETE */
#define AMP_INCOMPLETE     (AMP_OS_START_STATUS + 8)
/* empty slot: +9 */
/* empty slot: +10 */
/* empty slot: +11 */
/** @see AMP_ERR_IS_BADCH */
#define AMP_BADCH          (AMP_OS_START_STATUS + 12)
/** @see AMP_ERR_IS_BADARG */
#define AMP_BADARG         (AMP_OS_START_STATUS + 13)
/** @see AMP_ERR_IS_EOF */
#define AMP_EOF            (AMP_OS_START_STATUS + 14)
/** @see AMP_ERR_IS_NOTFOUND */
#define AMP_NOTFOUND       (AMP_OS_START_STATUS + 15)
/* empty slot: +16 */
/* empty slot: +17 */
/* empty slot: +18 */
/** @see AMP_ERR_IS_ANONYMOUS */
#define AMP_ANONYMOUS      (AMP_OS_START_STATUS + 19)
/** @see AMP_ERR_IS_FILEBASED */
#define AMP_FILEBASED      (AMP_OS_START_STATUS + 20)
/** @see AMP_ERR_IS_KEYBASED */
#define AMP_KEYBASED       (AMP_OS_START_STATUS + 21)
/** @see AMP_ERR_IS_EINIT */
#define AMP_EINIT          (AMP_OS_START_STATUS + 22)
/** @see AMP_ERR_IS_ENOTIMPL */
#define AMP_ENOTIMPL       (AMP_OS_START_STATUS + 23)
/** @see AMP_ERR_IS_EMISMATCH */
#define AMP_EMISMATCH      (AMP_OS_START_STATUS + 24)
/** @see AMP_ERR_IS_EBUSY */
#define AMP_EBUSY          (AMP_OS_START_STATUS + 25)
/** @} */

/**
* @addtogroup AMP_STATUS_IS
* @{
*/
/**
* Program is currently executing in the child
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code */
#define AMP_ERR_IS_INCHILD(e)        ((e)->status == AMP_INCHILD)
/**
* Program is currently executing in the parent
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_INPARENT(e)       ((e)->status == AMP_INPARENT)
/**
* The thread is detached
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_DETACH(e)         ((e)->status == AMP_DETACH)
/**
* The thread is not detached
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_NOTDETACH(e)      ((e)->status == AMP_NOTDETACH)
/**
* The child has finished executing
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_CHILD_DONE(e)     ((e)->status == AMP_CHILD_DONE)
/**
* The child has not finished executing
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_CHILD_NOTDONE(e)  ((e)->status == AMP_CHILD_NOTDONE)
/**
* The operation did not finish before the timeout
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_TIMEUP(e)         ((e)->status == AMP_TIMEUP)
/**
* The operation was incomplete although some processing was performed
* and the results are partially valid.
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_INCOMPLETE(e)     ((e)->status == AMP_INCOMPLETE)
/* empty slot: +9 */
/* empty slot: +10 */
/* empty slot: +11 */
/**
* Getopt found an option not in the option string
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_BADCH(e)          ((e)->status == AMP_BADCH)
/**
* Getopt found an option not in the option string and an argument was
* specified in the option string
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_BADARG(e)         ((e)->status == AMP_BADARG)
/**
* APR has encountered the end of the file
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_EOF(e)            ((e)->status == AMP_EOF)
/**
* APR was unable to find the socket in the poll structure
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_NOTFOUND(e)       ((e)->status == AMP_NOTFOUND)
/* empty slot: +16 */
/* empty slot: +17 */
/* empty slot: +18 */
/**
* APR is using anonymous shared memory
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_ANONYMOUS(e)      ((e)->status == AMP_ANONYMOUS)
/**
* APR is using a file name as the key to the shared memory
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_FILEBASED(e)      ((e)->status == AMP_FILEBASED)
/**
* APR is using a shared key as the key to the shared memory
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_KEYBASED(e)       ((e)->status == AMP_KEYBASED)
/**
* Ininitalizer value.  If no option has been found, but
* the status variable requires a value, this should be used
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_EINIT(e)          ((e)->status == AMP_EINIT)
/**
* The APR function has not been implemented on this
* platform, either because nobody has gotten to it yet,
* or the function is impossible on this platform.
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_ENOTIMPL(e)       ((e)->status == AMP_ENOTIMPL)
/**
* Two passwords do not match.
* @warning
* always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_EMISMATCH(e)      ((e)->status == AMP_EMISMATCH)
/**
* The given lock was busy
* @warning always use this test, as platform-specific variances may meet this
* more than one error code
*/
#define AMP_ERR_IS_EBUSY(e)          ((e)->status == AMP_EBUSY)

/** @} */

/**
* @addtogroup AMP_Error APR Error Values
* @{
*/
/* APR CANONICAL ERROR VALUES */
/** @see AMP_ERR_IS_EACCES */
#ifdef EACCES
#define AMP_EACCES EACCES
#else
#define AMP_EACCES         (AMP_OS_START_CANONERR + 1)
#endif

/** @see AMP_ERR_IS_EEXIST */
#ifdef EEXIST
#define AMP_EEXIST EEXIST
#else
#define AMP_EEXIST         (AMP_OS_START_CANONERR + 2)
#endif

/** @see AMP_ERR_IS_ENAMETOOLONG */
#ifdef ENAMETOOLONG
#define AMP_ENAMETOOLONG ENAMETOOLONG
#else
#define AMP_ENAMETOOLONG   (AMP_OS_START_CANONERR + 3)
#endif

/** @see AMP_ERR_IS_ENOENT */
#ifdef ENOENT
#define AMP_ENOENT ENOENT
#else
#define AMP_ENOENT         (AMP_OS_START_CANONERR + 4)
#endif

/** @see AMP_ERR_IS_ENOTDIR */
#ifdef ENOTDIR
#define AMP_ENOTDIR ENOTDIR
#else
#define AMP_ENOTDIR        (AMP_OS_START_CANONERR + 5)
#endif

/** @see AMP_ERR_IS_ENOSPC */
#ifdef ENOSPC
#define AMP_ENOSPC ENOSPC
#else
#define AMP_ENOSPC         (AMP_OS_START_CANONERR + 6)
#endif

/** @see AMP_ERR_IS_ENOMEM */
#ifdef ENOMEM
#define AMP_ENOMEM ENOMEM
#else
#define AMP_ENOMEM         (AMP_OS_START_CANONERR + 7)
#endif

/** @see AMP_ERR_IS_EMFILE */
#ifdef EMFILE
#define AMP_EMFILE EMFILE
#else
#define AMP_EMFILE         (AMP_OS_START_CANONERR + 8)
#endif

/** @see AMP_ERR_IS_ENFILE */
#ifdef ENFILE
#define AMP_ENFILE ENFILE
#else
#define AMP_ENFILE         (AMP_OS_START_CANONERR + 9)
#endif

/** @see AMP_ERR_IS_EBADF */
#ifdef EBADF
#define AMP_EBADF EBADF
#else
#define AMP_EBADF          (AMP_OS_START_CANONERR + 10)
#endif

/** @see AMP_ERR_IS_EINVAL */
#ifdef EINVAL
#define AMP_EINVAL EINVAL
#else
#define AMP_EINVAL         (AMP_OS_START_CANONERR + 11)
#endif

/** @see AMP_ERR_IS_ESPIPE */
#ifdef ESPIPE
#define AMP_ESPIPE ESPIPE
#else
#define AMP_ESPIPE         (AMP_OS_START_CANONERR + 12)
#endif

/**
* @see AMP_ERR_IS_EAGAIN
* @warning use AMP_ERR_IS_EAGAIN instead of just testing this value
*/
#ifdef EAGAIN
#define AMP_EAGAIN EAGAIN
#elif defined(EWOULDBLOCK)
#define AMP_EAGAIN EWOULDBLOCK
#else
#define AMP_EAGAIN         (AMP_OS_START_CANONERR + 13)
#endif

/** @see AMP_ERR_IS_EINTR */
#ifdef EINTR
#define AMP_EINTR EINTR
#else
#define AMP_EINTR          (AMP_OS_START_CANONERR + 14)
#endif

/** @see AMP_ERR_IS_ENOTSOCK */
#ifdef ENOTSOCK
#define AMP_ENOTSOCK ENOTSOCK
#else
#define AMP_ENOTSOCK       (AMP_OS_START_CANONERR + 15)
#endif

/** @see AMP_ERR_IS_ECONNREFUSED */
#ifdef ECONNREFUSED
#define AMP_ECONNREFUSED ECONNREFUSED
#else
#define AMP_ECONNREFUSED   (AMP_OS_START_CANONERR + 16)
#endif

/** @see AMP_ERR_IS_EINPROGRESS */
#ifdef EINPROGRESS
#define AMP_EINPROGRESS EINPROGRESS
#else
#define AMP_EINPROGRESS    (AMP_OS_START_CANONERR + 17)
#endif

/**
* @see AMP_ERR_IS_ECONNABORTED
* @warning use AMP_ERR_IS_ECONNABORTED instead of just testing this value
*/

#ifdef ECONNABORTED
#define AMP_ECONNABORTED ECONNABORTED
#else
#define AMP_ECONNABORTED   (AMP_OS_START_CANONERR + 18)
#endif

/** @see AMP_ERR_IS_ECONNRESET */
#ifdef ECONNRESET
#define AMP_ECONNRESET ECONNRESET
#else
#define AMP_ECONNRESET     (AMP_OS_START_CANONERR + 19)
#endif

/** @see AMP_ERR_IS_ETIMEDOUT
*  @deprecated */
#ifdef ETIMEDOUT
#define AMP_ETIMEDOUT ETIMEDOUT
#else
#define AMP_ETIMEDOUT      (AMP_OS_START_CANONERR + 20)
#endif

/** @see AMP_ERR_IS_EHOSTUNREACH */
#ifdef EHOSTUNREACH
#define AMP_EHOSTUNREACH EHOSTUNREACH
#else
#define AMP_EHOSTUNREACH   (AMP_OS_START_CANONERR + 21)
#endif

/** @see AMP_ERR_IS_ENETUNREACH */
#ifdef ENETUNREACH
#define AMP_ENETUNREACH ENETUNREACH
#else
#define AMP_ENETUNREACH    (AMP_OS_START_CANONERR + 22)
#endif

/** @see AMP_ERR_IS_EFTYPE */
#ifdef EFTYPE
#define AMP_EFTYPE EFTYPE
#else
#define AMP_EFTYPE        (AMP_OS_START_CANONERR + 23)
#endif

/** @see AMP_ERR_IS_EPIPE */
#ifdef EPIPE
#define AMP_EPIPE EPIPE
#else
#define AMP_EPIPE         (AMP_OS_START_CANONERR + 24)
#endif

/** @see AMP_ERR_IS_EXDEV */
#ifdef EXDEV
#define AMP_EXDEV EXDEV
#else
#define AMP_EXDEV         (AMP_OS_START_CANONERR + 25)
#endif

/** @see AMP_ERR_IS_ENOTEMPTY */
#ifdef ENOTEMPTY
#define AMP_ENOTEMPTY ENOTEMPTY
#else
#define AMP_ENOTEMPTY     (AMP_OS_START_CANONERR + 26)
#endif

/** @see AMP_ERR_IS_EAFNOSUPPORT */
#ifdef EAFNOSUPPORT
#define AMP_EAFNOSUPPORT EAFNOSUPPORT
#else
#define AMP_EAFNOSUPPORT  (AMP_OS_START_CANONERR + 27)
#endif

/** @see AMP_ERR_IS_EOPNOTSUPP */
#ifdef EOPNOTSUPP
#define AMP_EOPNOTSUPP EOPNOTSUPP
#else
#define AMP_EOPNOTSUPP    (AMP_OS_START_CANONERR + 28)
#endif

/** @see AMP_ERR_IS_ERANGE */
#ifdef ERANGE
#define AMP_ERANGE ERANGE
#else
#define AMP_ERANGE          (AMP_OS_START_CANONERR + 29)
#endif

/** @} */

#if defined(WIN32) && !defined(DOXYGEN)

#define AMP_ERR_FROM_OS(e) (e == 0 ? AMP_SUCCESS : e + AMP_OS_START_SYSERR)
#define AMP_ERR_TO_OS(e)   (e == 0 ? AMP_SUCCESS : e - AMP_OS_START_SYSERR)

#define amp_err_get_os()   (AMP_ERR_FROM_OS(GetLastError()))
#define amp_err_set_os(e)  (SetLastError(AMP_ERR_TO_OS(e)))

/* A special case, only socket calls require this:
*/
#define amp_err_get_net_os()   (AMP_ERR_FROM_OS(WSAGetLastError()))
#define amp_err_set_net_os(e)   (WSASetLastError(AMP_ERR_TO_OS(e)))

/* APR CANONICAL ERROR TESTS */
#define AMP_ERR_IS_EACCES(e)         ((e)->status == AMP_EACCES \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_ACCESS_DENIED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_CANNOT_MAKE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_CURRENT_DIRECTORY \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_DRIVE_LOCKED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_FAIL_I24 \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_LOCK_VIOLATION \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_LOCK_FAILED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NOT_LOCKED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NETWORK_ACCESS_DENIED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_SHARING_VIOLATION)
#define AMP_ERR_IS_EEXIST(e)         ((e)->status == AMP_EEXIST \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_FILE_EXISTS \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_ALREADY_EXISTS)
#define AMP_ERR_IS_ENAMETOOLONG(e)   ((e)->status == AMP_ENAMETOOLONG \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_FILENAME_EXCED_RANGE \
                || (e)->status == AMP_OS_START_SYSERR + WSAENAMETOOLONG)
#define AMP_ERR_IS_ENOENT(e)         ((e)->status == AMP_ENOENT \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_FILE_NOT_FOUND \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_PATH_NOT_FOUND \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_OPEN_FAILED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NO_MORE_FILES)
#define AMP_ERR_IS_ENOTDIR(e)        ((e)->status == AMP_ENOTDIR \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_PATH_NOT_FOUND \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_BAD_NETPATH \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_BAD_NET_NAME \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_BAD_PATHNAME \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_DRIVE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_DIRECTORY)
#define AMP_ERR_IS_ENOSPC(e)         ((e)->status == AMP_ENOSPC \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_DISK_FULL)
#define AMP_ERR_IS_ENOMEM(e)         ((e)->status == AMP_ENOMEM \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_ARENA_TRASHED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NOT_ENOUGH_MEMORY \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_BLOCK \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NOT_ENOUGH_QUOTA \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_OUTOFMEMORY)
#define AMP_ERR_IS_EMFILE(e)         ((e)->status == AMP_EMFILE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_TOO_MANY_OPEN_FILES)
#define AMP_ERR_IS_ENFILE(e)         ((e)->status == AMP_ENFILE)
#define AMP_ERR_IS_EBADF(e)          ((e)->status == AMP_EBADF \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_HANDLE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_TARGET_HANDLE)
#define AMP_ERR_IS_EINVAL(e)         ((e)->status == AMP_EINVAL \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_ACCESS \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_DATA \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_FUNCTION \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_HANDLE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_PARAMETER \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NEGATIVE_SEEK)
#define AMP_ERR_IS_ESPIPE(e)         ((e)->status == AMP_ESPIPE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_SEEK_ON_DEVICE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NEGATIVE_SEEK)
#define AMP_ERR_IS_EAGAIN(e)         ((e)->status == AMP_EAGAIN \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NO_DATA \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NO_PROC_SLOTS \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NESTING_NOT_ALLOWED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_MAX_THRDS_REACHED \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_LOCK_VIOLATION \
                || (e)->status == AMP_OS_START_SYSERR + WSAEWOULDBLOCK)
#define AMP_ERR_IS_EINTR(e)          ((e)->status == AMP_EINTR \
                || (e)->status == AMP_OS_START_SYSERR + WSAEINTR)
#define AMP_ERR_IS_ENOTSOCK(e)       ((e)->status == AMP_ENOTSOCK \
                || (e)->status == AMP_OS_START_SYSERR + WSAENOTSOCK)
#define AMP_ERR_IS_ECONNREFUSED(e)   ((e)->status == AMP_ECONNREFUSED \
                || (e)->status == AMP_OS_START_SYSERR + WSAECONNREFUSED)
#define AMP_ERR_IS_EINPROGRESS(e)    ((e)->status == AMP_EINPROGRESS \
                || (e)->status == AMP_OS_START_SYSERR + WSAEINPROGRESS)
#define AMP_ERR_IS_ECONNABORTED(e)   ((e)->status == AMP_ECONNABORTED \
                || (e)->status == AMP_OS_START_SYSERR + WSAECONNABORTED)
#define AMP_ERR_IS_ECONNRESET(e)     ((e)->status == AMP_ECONNRESET \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NETNAME_DELETED \
                || (e)->status == AMP_OS_START_SYSERR + WSAECONNRESET)
/* XXX deprecated */
#define AMP_ERR_IS_ETIMEDOUT(e)         ((e)->status == AMP_ETIMEDOUT \
                || (e)->status == AMP_OS_START_SYSERR + WSAETIMEDOUT \
                || (e)->status == AMP_OS_START_SYSERR + WAIT_TIMEOUT)
#undef AMP_ERR_IS_TIMEUP
#define AMP_ERR_IS_TIMEUP(e)         ((e)->status == AMP_TIMEUP \
                || (e)->status == AMP_OS_START_SYSERR + WSAETIMEDOUT \
                || (e)->status == AMP_OS_START_SYSERR + WAIT_TIMEOUT)
#define AMP_ERR_IS_EHOSTUNREACH(e)   ((e)->status == AMP_EHOSTUNREACH \
                || (e)->status == AMP_OS_START_SYSERR + WSAEHOSTUNREACH)
#define AMP_ERR_IS_ENETUNREACH(e)    ((e)->status == AMP_ENETUNREACH \
                || (e)->status == AMP_OS_START_SYSERR + WSAENETUNREACH)
#define AMP_ERR_IS_EFTYPE(e)         ((e)->status == AMP_EFTYPE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_EXE_MACHINE_TYPE_MISMATCH \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_DLL \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_MODULETYPE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_BAD_EXE_FORMAT \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_INVALID_EXE_SIGNATURE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_FILE_CORRUPT \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_BAD_FORMAT)
#define AMP_ERR_IS_EPIPE(e)          ((e)->status == AMP_EPIPE \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_BROKEN_PIPE)
#define AMP_ERR_IS_EXDEV(e)          ((e)->status == AMP_EXDEV \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_NOT_SAME_DEVICE)
#define AMP_ERR_IS_ENOTEMPTY(e)      ((e)->status == AMP_ENOTEMPTY \
                || (e)->status == AMP_OS_START_SYSERR + ERROR_DIR_NOT_EMPTY)
#define AMP_ERR_IS_EAFNOSUPPORT(e)   ((e)->status == AMP_EAFNOSUPPORT \
                || (e)->status == AMP_OS_START_SYSERR + WSAEAFNOSUPPORT)
#define AMP_ERR_IS_EOPNOTSUPP(e)     ((e)->status == AMP_EOPNOTSUPP \
                || (e)->status == AMP_OS_START_SYSERR + WSAEOPNOTSUPP)
#define AMP_ERR_IS_ERANGE(e)         ((e)->status == AMP_ERANGE)
#else 

/*
*  os error codes are clib error codes
*/
#define AMP_ERR_FROM_OS(e)  (e)
#define AMP_ERR_TO_OS(e)    (e)

#define amp_err_get_os()    (errno)
#define amp_err_set_os(e)   (errno = (e))

/* A special case, only socket calls require this:
*/
#define amp_err_get_net_os() (errno)
#define amp_err_set_net_os(e) (errno = (e))

/**
* @addtogroup AMP_STATUS_IS
* @{
*/

/** permission denied */
#define AMP_ERR_IS_EACCES(e)         ((e)->status == AMP_EACCES)
/** file exists */
#define AMP_ERR_IS_EEXIST(e)         ((e)->status == AMP_EEXIST)
/** path name is too long */
#define AMP_ERR_IS_ENAMETOOLONG(e)   ((e)->status == AMP_ENAMETOOLONG)
/**
* no such file or directory
* @remark
* EMVSCATLG can be returned by the automounter on z/OS for
* paths which do not exist.
*/
#ifdef EMVSCATLG
#define AMP_ERR_IS_ENOENT(e)         ((e)->status == AMP_ENOENT \
                                      || (e)->status == EMVSCATLG)
#else
#define AMP_ERR_IS_ENOENT(e)         ((e)->status == AMP_ENOENT)
#endif
/** not a directory */
#define AMP_ERR_IS_ENOTDIR(e)        ((e)->status == AMP_ENOTDIR)
/** no space left on device */
#ifdef EDQUOT
#define AMP_ERR_IS_ENOSPC(e)         ((e)->status == AMP_ENOSPC \
                                      || (e)->status == EDQUOT)
#else
#define AMP_ERR_IS_ENOSPC(e)         ((e)->status == AMP_ENOSPC)
#endif
/** not enough memory */
#define AMP_ERR_IS_ENOMEM(e)         ((e)->status == AMP_ENOMEM)
/** too many open files */
#define AMP_ERR_IS_EMFILE(e)         ((e)->status == AMP_EMFILE)
/** file table overflow */
#define AMP_ERR_IS_ENFILE(e)         ((e)->status == AMP_ENFILE)
/** bad file # */
#define AMP_ERR_IS_EBADF(e)          ((e)->status == AMP_EBADF)
/** invalid argument */
#define AMP_ERR_IS_EINVAL(e)         ((e)->status == AMP_EINVAL)
/** illegal seek */
#define AMP_ERR_IS_ESPIPE(e)         ((e)->status == AMP_ESPIPE)

/** operation would block */
#if !defined(EWOULDBLOCK) || !defined(EAGAIN)
#define AMP_ERR_IS_EAGAIN(e)         ((e)->status == AMP_EAGAIN)
#elif (EWOULDBLOCK == EAGAIN)
#define AMP_ERR_IS_EAGAIN(e)         ((e)->status == AMP_EAGAIN)
#else
#define AMP_ERR_IS_EAGAIN(e)         ((e)->status == AMP_EAGAIN \
                                      || (e)->status == EWOULDBLOCK)
#endif

/** interrupted system call */
#define AMP_ERR_IS_EINTR(e)          ((e)->status == AMP_EINTR)
/** socket operation on a non-socket */
#define AMP_ERR_IS_ENOTSOCK(e)       ((e)->status == AMP_ENOTSOCK)
/** Connection Refused */
#define AMP_ERR_IS_ECONNREFUSED(e)   ((e)->status == AMP_ECONNREFUSED)
/** operation now in progress */
#define AMP_ERR_IS_EINPROGRESS(e)    ((e)->status == AMP_EINPROGRESS)

/**
* Software caused connection abort
* @remark
* EPROTO on certain older kernels really means ECONNABORTED, so we need to
* ignore it for them.  See discussion in new-httpd archives nh.9701 & nh.9603
*
* There is potentially a bug in Solaris 2.x x<6, and other boxes that
* implement tcp sockets in userland (i.e. on top of STREAMS).  On these
* systems, EPROTO can actually result in a fatal loop.  See PR#981 for
* example.  It's hard to handle both uses of EPROTO.
*/
#ifdef EPROTO
#define AMP_ERR_IS_ECONNABORTED(e)    ((e)->status == AMP_ECONNABORTED \
                                       || (e)->status == EPROTO)
#else
#define AMP_ERR_IS_ECONNABORTED(e)    ((e)->status == AMP_ECONNABORTED)
#endif

/** Connection Reset by peer */
#define AMP_ERR_IS_ECONNRESET(e)      ((e)->status == AMP_ECONNRESET)
/** Operation timed out
*  @deprecated */
#define AMP_ERR_IS_ETIMEDOUT(e)      ((e)->status == AMP_ETIMEDOUT)
/** no route to host */
#define AMP_ERR_IS_EHOSTUNREACH(e)    ((e)->status == AMP_EHOSTUNREACH)
/** network is unreachable */
#define AMP_ERR_IS_ENETUNREACH(e)     ((e)->status == AMP_ENETUNREACH)
/** inappropriate file type or format */
#define AMP_ERR_IS_EFTYPE(e)          ((e)->status == AMP_EFTYPE)
/** broken pipe */
#define AMP_ERR_IS_EPIPE(e)           ((e)->status == AMP_EPIPE)
/** cross device link */
#define AMP_ERR_IS_EXDEV(e)           ((e)->status == AMP_EXDEV)
/** Directory Not Empty */
#define AMP_ERR_IS_ENOTEMPTY(e)       ((e)->status == AMP_ENOTEMPTY || \
                                          (e)->status == AMP_EEXIST)
/** Address Family not supported */
#define AMP_ERR_IS_EAFNOSUPPORT(e)    ((e)->status == AMP_EAFNOSUPPORT)
/** Socket operation not supported */
#define AMP_ERR_IS_EOPNOTSUPP(e)      ((e)->status == AMP_EOPNOTSUPP)

/** Numeric value not representable */
#define AMP_ERR_IS_ERANGE(e)         ((e)->status == AMP_ERANGE)
/** @} */

#endif /* !defined(WIN32) */

/** @} */

AMP_C__END
