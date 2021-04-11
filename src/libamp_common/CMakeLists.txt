# amp_subr: Common library for the AmpScm project
add_library(libamp_common
	common.cxx
	io.cxx
	error.cxx) 

target_include_directories(libamp_common
	PUBLIC ../include
	PRIVATE ../include/private)
