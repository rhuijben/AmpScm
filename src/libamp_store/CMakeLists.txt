# amp_subr: Common library for the AmpScm project
add_library(libamp_store
	store.cxx)

target_include_directories(libamp_store
	PUBLIC ../include
	PRIVATE ../include/private)
