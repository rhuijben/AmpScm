add_library(libamp_src_subversion
  src_subversion.cxx) 

  
target_include_directories(libamp_src_subversion
	PUBLIC ../include
	PRIVATE ../include/private)
