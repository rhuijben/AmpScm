# AmpScm

Core library to empower("amplify") Source Code management tooling.

First step is writing an Apache Serf/Subversion like api that using minimal copy streams allows access to git repositories and its datastructures (like commit graphs).

This code deliberately doesn't use libgit2 and the standard git libraries as those are mostly designed to be used in GPL and short living processes with very limited error handling.
The design of this library tries to get the same level of library error handling as Subversion, while using the pull semantics as used in Apache serf to access datastructures as memory safe streams,
that don't require the full memory mapping of git objects as the other libraries do.

The current api already allows using reading through packed git objects of theoretically many GBs using very limited memory.

The project aims to produce code that is compatible with at least Windows, Linux, OS/x. (x86, x86_64, ARM and ARM64 compatible). Designed to be used inside other products and libraries. In particular to be wrapped by .Net code.
