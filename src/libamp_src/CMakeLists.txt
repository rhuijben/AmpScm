add_library(libamp_src src.cxx) 


target_include_directories(libamp_src
	PUBLIC ../include
	PRIVATE ../include/private)
