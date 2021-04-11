add_library(libamp_store_git
   store_git.cxx)

target_include_directories(libamp_store_git
	PUBLIC ../include
	PRIVATE ../include/private)
