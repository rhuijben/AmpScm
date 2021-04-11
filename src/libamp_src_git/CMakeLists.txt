add_library(libamp_src_git src_git.cxx) 


target_include_directories(libamp_src_git
	PUBLIC ../include
	PRIVATE ../include/private)
